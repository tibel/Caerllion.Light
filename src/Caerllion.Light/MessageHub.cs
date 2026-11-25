using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Caerllion.Light;

/// <summary>
/// Provides in-process publish-subscribe and request-reply message handling.
/// </summary>
public sealed class MessageHub : IAsyncDisposable
{
    private readonly Channel<object> _messages = Channel.CreateUnbounded<object>(
        new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly List<IMessageHandler> _handlers = new();
    private readonly Task _completion;
    private int _lastHandlerId;

    /// <summary>Creates an empty message hub.</summary>
    public MessageHub()
    {
        _completion = HandleMessagesAsync();
    }

    /// <summary>
    /// Gets a task that completes after the hub has drained and its handlers have been cleared.
    /// </summary>
    public Task Completion => _completion;

    /// <summary>
    /// Stops accepting messages and drains all messages already written.
    /// </summary>
    public bool Complete() => _messages.Writer.TryComplete();

    /// <summary>
    /// Completes the hub and waits until the dispatch loop has drained.
    /// </summary>
    /// <remarks>Cancellation stops waiting; it does not stop the hub from draining its queue.</remarks>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        Complete();
        await _completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Raised when a handler throws or a handler task faults.
    /// </summary>
    /// <remarks>Exceptions thrown by event subscribers are swallowed so they cannot stop dispatch.</remarks>
    public event EventHandler<MessageHandlingEventArgs>? Error;

    /// <summary>
    /// Raised when a published message has no handler.
    /// </summary>
    /// <remarks>Exceptions thrown by event subscribers are swallowed so they cannot stop dispatch.</remarks>
    public event EventHandler<MessageHandlingEventArgs>? Unhandled;

    private void OnError(object message, Exception exception)
    {
        try
        {
            Error?.Invoke(this, new MessageHandlingEventArgs(message, exception));
        }
        catch
        {
            // Event subscribers must not destabilize the dispatch loop.
        }
    }

    private void OnMessageNotHandled(object message)
    {
        try
        {
            Unhandled?.Invoke(this, new MessageHandlingEventArgs(message, exception: null));
        }
        catch
        {
            // Event subscribers must not destabilize the dispatch loop.
        }
    }

    private async Task HandleMessagesAsync()
    {
        await foreach (var message in _messages.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await HandleMessageAsync(message).ConfigureAwait(false);
        }

        _handlers.Clear();
    }

    private async Task HandleMessageAsync(object message)
    {
        if (message is InvokeMessage canceledInvoke &&
            canceledInvoke.CancellationToken.IsCancellationRequested)
        {
            canceledInvoke.SetCanceled();
            return;
        }

        if (message is IMessageHandler addHandler)
        {
            _handlers.Add(addHandler);
            return;
        }

        if (message is RemoveHandlerMessage removeHandler)
        {
            _handlers.RemoveAll(handler => handler.Id == removeHandler.Id);
            return;
        }

        var handled = false;
        foreach (var handler in _handlers)
        {
            if (await handler.TryHandleAsync(message).ConfigureAwait(false))
                handled = true;
        }

        if (!handled)
        {
            if (message is InvokeMessage invokeMessage)
                invokeMessage.SetCanceled();
            else
                OnMessageNotHandled(message);
        }
    }

    private int GetNextHandlerId()
    {
        return Interlocked.Increment(ref _lastHandlerId);
    }

    private Subscription Subscribe(IMessageHandler handler)
    {
        if (!Publish(handler))
            throw new InvalidOperationException("Cannot subscribe to a completed message hub.");

        return new Subscription(handler.Id, this);
    }

    /// <summary>Subscribes a synchronous typed message handler.</summary>
    public Subscription Subscribe<TMessage>(Action<TMessage> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(new MessageHandler<TMessage>(GetNextHandlerId(), handler, OnError));
    }

    /// <summary>Subscribes an asynchronous typed message handler.</summary>
    public Subscription SubscribeAsync<TMessage>(Func<TMessage, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(new MessageHandlerAsync<TMessage>(GetNextHandlerId(), handler, OnError));
    }

    /// <summary>Subscribes a synchronous request handler.</summary>
    public Subscription Subscribe<TRequest, TReply>(Func<TRequest, TReply> handler)
        where TRequest : IRequest<TReply>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(new InvokeMessageHandler<TRequest, TReply>(GetNextHandlerId(), handler));
    }

    /// <summary>Subscribes an asynchronous request handler.</summary>
    public Subscription SubscribeAsync<TRequest, TReply>(Func<TRequest, Task<TReply>> handler)
        where TRequest : IRequest<TReply>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(new InvokeMessageHandlerAsync<TRequest, TReply>(GetNextHandlerId(), handler));
    }

    /// <summary>Subscribes a cancellation-aware asynchronous request handler.</summary>
    public Subscription SubscribeAsync<TRequest, TReply>(Func<TRequest, CancellationToken, Task<TReply>> handler)
        where TRequest : IRequest<TReply>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(new InvokeMessageHandlerAsync<TRequest, TReply>(GetNextHandlerId(), handler));
    }

    /// <summary>Subscribes a generic handler that receives every message.</summary>
    public Subscription Subscribe(Func<object, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(new GenericMessageHandler(GetNextHandlerId(), handler, OnError));
    }

    /// <summary>
    /// Publishes a message to the hub.
    /// </summary>
    public bool Publish(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _messages.Writer.TryWrite(message);
    }

    /// <summary>
    /// Sends a request, returns its reply, and observes request cancellation.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the request. Defaults to no cancellation.</param>
    public Task<TReply> InvokeAsync<TRequest, TReply>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TReply>
    {
        var invokeMessage = new InvokeMessage<TRequest, TReply>(request, cancellationToken);
        if (!Publish(invokeMessage))
            invokeMessage.SetCanceled();
        return invokeMessage.Completion;
    }

    /// <summary>Completes and drains the message hub.</summary>
    public async ValueTask DisposeAsync()
    {
        await CompleteAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    public sealed class MessageHub : IDisposable
    {
        private readonly Channel<object> _messages = Channel.CreateUnbounded<object>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });

        private readonly List<IHandler> _handlers = new List<IHandler>();
        private int _lastHandlerId;

        public MessageHub()
        {
            HandleMessages();
        }

        public void Dispose()
        {
            _messages.Writer.TryComplete();
        }

        private void OnError(Exception ex)
        {
            //TODO error on handle message
        }

        private void OnMessageNotHandled(object message)
        {
            //TODO message not handled
        }

        private async void HandleMessages()
        {
            while (await _messages.Reader.WaitToReadAsync().ConfigureAwait(false))
                HandleMessagesCore();

            _handlers.Clear();
        }

        private void HandleMessagesCore()
        {
            while (_messages.Reader.TryRead(out var message))
            {
                if (message is IHandler addHandler)
                {
                    _handlers.Add(addHandler);
                }
                else if (message is RemoveHandler removeHandler)
                {
                    _handlers.RemoveAll(h => h.Id == removeHandler.Id);
                }
                else
                {
                    var handled = false;
                    foreach (var handler in _handlers)
                    {
                        if (handler.TryHandle(message))
                            handled = true;
                    }

                    if (!handled)
                    {
                        if (message is IInvokeMethod invokeMethod)
                            invokeMethod.EnsureTaskCompleted();
                        else
                            OnMessageNotHandled(message);
                    }
                }
            }
        }

        public Subscription Subscribe<TMessage>(Action<TMessage> handler)
        {
            var id = Interlocked.Increment(ref _lastHandlerId);
            var h = new MessageSyncHandler<TMessage>(id, handler, OnError);
            _messages.Writer.TryWrite(h);
            return new Subscription(h.Id, this);
        }

        public Subscription Subscribe<TMessage>(Func<TMessage, Task> handler)
        {
            var id = Interlocked.Increment(ref _lastHandlerId);
            var h = new MessageAsyncHandler<TMessage>(id, handler, OnError);
            _messages.Writer.TryWrite(h);
            return new Subscription(h.Id, this);
        }

        public Subscription Subscribe<TRequest, TReply>(Func<TRequest, TReply> handler)
            where TRequest : IRequest<TReply>
        {
            var id = Interlocked.Increment(ref _lastHandlerId);
            var h = new InvokeMethodSyncHandler<TRequest, TReply>(id, handler);
            _messages.Writer.TryWrite(h);
            return new Subscription(h.Id, this);
        }

        public Subscription Subscribe<TRequest, TReply>(Func<TRequest, Task<TReply>> handler)
            where TRequest : IRequest<TReply>
        {
            var id = Interlocked.Increment(ref _lastHandlerId);
            var h = new InvokeMethodAsyncHandler<TRequest, TReply>(id, handler);
            _messages.Writer.TryWrite(h);
            return new Subscription(h.Id, this);
        }

        internal void Unsubscibe(int id)
        {
            _messages.Writer.TryWrite(new RemoveHandler(id));
        }

        public void Publish(object message)
        {
            _messages.Writer.TryWrite(message);
        }

        public Task<TReply> InvokeAsync<TRequest, TReply>(TRequest request)
            where TRequest : IRequest<TReply>
        {
            var invokeMethod = new InvokeMethod<TRequest, TReply>(request);
            if (!_messages.Writer.TryWrite(invokeMethod))
                invokeMethod.ReplySource.TrySetCanceled();
            return invokeMethod.ReplySource.Task;
        }
    }
}

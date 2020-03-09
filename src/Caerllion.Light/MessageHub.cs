using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    public sealed class MessageHub
    {
        private readonly Channel<object> _messages = Channel.CreateUnbounded<object>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });

        private readonly List<IMessageHandler> _handlers = new List<IMessageHandler>();
        private int _lastHandlerId;

        public MessageHub()
        {
            HandleMessages();
        }

        public bool Complete() => _messages.Writer.TryComplete();

        public event EventHandler<MessageHandlingEventArgs> Error;

        public event EventHandler<MessageHandlingEventArgs> Unhandled;

        private void OnError(object message, Exception exception) => Error?.Invoke(this, new MessageHandlingEventArgs(message, exception));

        private void OnMessageNotHandled(object message) => Unhandled?.Invoke(this, new MessageHandlingEventArgs(message, null));

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
                if (message is IMessageHandler addHandler)
                {
                    _handlers.Add(addHandler);
                }
                else if (message is RemoveHandlerMessage removeHandler)
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
                        if (message is InvokeMessage invokeMessage)
                            invokeMessage.SetCanceled();
                        else
                            OnMessageNotHandled(message);
                    }
                }
            }
        }

        private int GetNextHandlerId()
        {
            return Interlocked.Increment(ref _lastHandlerId);
        }

        private Subscription Subscibe(IMessageHandler handler)
        {
            Publish(handler);
            return new Subscription(handler.Id, this);
        }

        public Subscription Subscribe<TMessage>(Action<TMessage> handler) => Subscibe(new MessageHandler<TMessage>(GetNextHandlerId(), handler, OnError));

        public Subscription Subscribe<TMessage>(Func<TMessage, Task> handler) => Subscibe(new MessageHandlerAsync<TMessage>(GetNextHandlerId(), handler, OnError));

        public Subscription Subscribe<TRequest, TReply>(Func<TRequest, TReply> handler)
            where TRequest : IRequest<TReply> => Subscibe(new InvokeMessageHandler<TRequest, TReply>(GetNextHandlerId(), handler));

        public Subscription Subscribe<TRequest, TReply>(Func<TRequest, Task<TReply>> handler)
            where TRequest : IRequest<TReply> => Subscibe(new InvokeMessageHandlerAsync<TRequest, TReply>(GetNextHandlerId(), handler));

        public Subscription Subscribe(Func<object, bool> handler) => Subscibe(new GenericMessageHandler(GetNextHandlerId(), handler, OnError));

        public bool Publish(object message)
        {
            return _messages.Writer.TryWrite(message);
        }

        public Task<TReply> InvokeAsync<TRequest, TReply>(TRequest request)
            where TRequest : IRequest<TReply>
        {
            var invokeMethod = new InvokeMessage<TRequest, TReply>(request);
            if (!Publish(invokeMethod))
                invokeMethod.SetCanceled();
            return invokeMethod.Completion;
        }
    }
}

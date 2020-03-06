using System;

namespace Caerllion.Light
{
    internal sealed class MessageHandler<TMessage> : IMessageHandler
    {
        private readonly Action<TMessage> _handler;
        private readonly Action<object, Exception> _onError;

        public MessageHandler(int id, Action<TMessage> handler, Action<object, Exception> onError)
        {
            Id = id;
            _handler = handler;
            _onError = onError;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            return message is TMessage m && Handle(m);
        }

        private bool Handle(TMessage message)
        {
            try
            {
                _handler(message);
            }
            catch (Exception ex)
            {
                _onError(message, ex);
            }

            return true;
        }
    }
}

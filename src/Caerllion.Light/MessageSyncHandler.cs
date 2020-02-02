using System;

namespace Caerllion.Light
{
    internal sealed class MessageSyncHandler<TMessage> : IHandler
    {
        private readonly Action<TMessage> _handler;
        private readonly Action<Exception> _onError;

        public MessageSyncHandler(int id, Action<TMessage> handler, Action<Exception> onError)
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
                _onError(ex);
            }

            return true;
        }
    }
}

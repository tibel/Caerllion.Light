using System;

namespace Caerllion.Light
{
    internal sealed class GenericMessageHandler : IMessageHandler
    {
        private readonly Func<object, bool> _handler;
        private readonly Action<object, Exception> _onError;

        public GenericMessageHandler(int id, Func<object, bool> handler, Action<object, Exception> onError)
        {
            Id = id;
            _handler = handler;
            _onError = onError;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            try
            {
                return _handler(message);
            }
            catch (Exception ex)
            {
                _onError(message, ex);
                return false;
            }
        }
    }
}

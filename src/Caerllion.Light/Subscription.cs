using System;

namespace Caerllion.Light
{
    public readonly struct Subscription : IDisposable
    {
        private readonly int _id;
        private readonly MessageHub _messageHub;

        public Subscription(int id, MessageHub messageHub)
        {
            _id = id;
            _messageHub = messageHub;
        }

        public void Dispose()
        {
            _messageHub?.Publish(new RemoveHandlerMessage(_id));
        }
    }
}

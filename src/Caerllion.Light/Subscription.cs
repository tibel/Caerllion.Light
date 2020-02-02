using System;

namespace Caerllion.Light
{
    public struct Subscription : IDisposable
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
            _messageHub?.Unsubscibe(_id);
        }
    }
}

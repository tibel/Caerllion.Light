using System;

namespace Caerllion.Light
{
    public sealed class MessageHandlingEventArgs : EventArgs
    {
        public MessageHandlingEventArgs(object message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public object Message { get; }

        public Exception Exception { get; }
    }
}

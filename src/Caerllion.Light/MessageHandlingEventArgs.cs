using System;

namespace Caerllion.Light;

/// <summary>Contains message and exception details for hub events.</summary>
public sealed class MessageHandlingEventArgs : EventArgs
{
    /// <summary>Creates event arguments for a message handling notification.</summary>
    public MessageHandlingEventArgs(object message, Exception? exception)
    {
        Message = message;
        Exception = exception;
    }

    /// <summary>Gets the message associated with the notification.</summary>
    public object Message { get; }

    /// <summary>Gets the exception, or null for an unhandled message.</summary>
    public Exception? Exception { get; }
}

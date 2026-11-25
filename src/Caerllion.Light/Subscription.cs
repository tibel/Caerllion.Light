using System;

namespace Caerllion.Light;

/// <summary>Lifetime handle for a message hub subscription.</summary>
public readonly struct Subscription : IDisposable
{
    private readonly int _id;
    private readonly MessageHub? _messageHub;

    /// <summary>Creates a subscription handle.</summary>
    internal Subscription(int id, MessageHub messageHub)
    {
        _id = id;
        _messageHub = messageHub;
    }

    /// <summary>Queues removal of the subscription from its message hub.</summary>
    /// <remarks>Messages already ahead of the removal in the hub queue may still be delivered.</remarks>
    public void Dispose()
    {
        _messageHub?.Publish(new RemoveHandlerMessage(_id));
    }
}

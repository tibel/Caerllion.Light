using System;
using System.Threading.Tasks;

namespace Caerllion.Light;

internal sealed class InvokeMessageHandler<TRequest, TReply> : IMessageHandler
{
    private readonly Func<TRequest, TReply> _handler;

    public InvokeMessageHandler(int id, Func<TRequest, TReply> handler)
    {
        Id = id;
        _handler = handler;
    }

    public int Id { get; }

    public Task<bool> TryHandleAsync(object message)
    {
        return Task.FromResult(message is InvokeMessage<TRequest, TReply> m && Handle(m));
    }

    private bool Handle(InvokeMessage<TRequest, TReply> message)
    {
        if (!message.TryBeginHandle())
            return false;

        try
        {
            var result = _handler.Invoke(message.Request);
            message.SetResult(result);
        }
        catch (OperationCanceledException)
        {
            message.SetCanceled();
        }
        catch (Exception ex)
        {
            message.SetException(ex);
        }

        return true;
    }
}

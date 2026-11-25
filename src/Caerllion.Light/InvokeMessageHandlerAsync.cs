using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caerllion.Light;

internal sealed class InvokeMessageHandlerAsync<TRequest, TReply> : IMessageHandler
{
    private readonly Func<TRequest, CancellationToken, Task<TReply>> _handler;

    public InvokeMessageHandlerAsync(int id, Func<TRequest, Task<TReply>> handler)
        : this(id, (request, _) => handler(request))
    {
    }

    public InvokeMessageHandlerAsync(int id, Func<TRequest, CancellationToken, Task<TReply>> handler)
    {
        Id = id;
        _handler = handler;
    }

    public int Id { get; }

    public Task<bool> TryHandleAsync(object message)
    {
        return message is InvokeMessage<TRequest, TReply> m
            ? HandleAsync(m)
            : Task.FromResult(false);
    }

    private async Task<bool> HandleAsync(InvokeMessage<TRequest, TReply> message)
    {
        if (!message.TryBeginHandle())
            return false;

        try
        {
            var result = await _handler.Invoke(message.Request, message.CancellationToken).ConfigureAwait(false);
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

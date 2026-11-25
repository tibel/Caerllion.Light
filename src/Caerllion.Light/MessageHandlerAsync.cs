using System;
using System.Threading.Tasks;

namespace Caerllion.Light;

internal sealed class MessageHandlerAsync<TMessage> : IMessageHandler
{
    private readonly Func<TMessage, Task> _handler;
    private readonly Action<object, Exception> _onError;

    public MessageHandlerAsync(int id, Func<TMessage, Task> handler, Action<object, Exception> onError)
    {
        Id = id;
        _handler = handler;
        _onError = onError;
    }

    public int Id { get; }

    public async Task<bool> TryHandleAsync(object message)
    {
        if (message is not TMessage m)
            return false;

        await HandleAsync(m).ConfigureAwait(false);
        return true;
    }

    private async Task HandleAsync(TMessage message)
    {
        try
        {
            await _handler(message).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _onError((object)message!, ex);
        }
        catch (Exception ex)
        {
            _onError((object)message!, ex);
        }
    }
}

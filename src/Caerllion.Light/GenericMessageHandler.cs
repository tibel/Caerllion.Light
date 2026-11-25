using System;
using System.Threading.Tasks;

namespace Caerllion.Light;

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

    public Task<bool> TryHandleAsync(object message)
    {
        try
        {
            return Task.FromResult(_handler(message));
        }
        catch (Exception ex)
        {
            _onError(message, ex);
            return Task.FromResult(false);
        }
    }
}

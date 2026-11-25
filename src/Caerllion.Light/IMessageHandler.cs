using System.Threading.Tasks;

namespace Caerllion.Light;

internal interface IMessageHandler
{
    int Id { get; }

    Task<bool> TryHandleAsync(object message);
}

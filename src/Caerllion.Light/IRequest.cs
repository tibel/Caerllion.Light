namespace Caerllion.Light;

/// <summary>Marks a message as a request that has a reply of <typeparamref name="TReply"/>.</summary>
public interface IRequest<out TReply>
{
}

namespace Caerllion.Light
{
    internal interface IMessageHandler
    {
        int Id { get; }

        bool TryHandle(object message);
    }
}

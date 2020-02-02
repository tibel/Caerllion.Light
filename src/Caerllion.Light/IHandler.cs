namespace Caerllion.Light
{
    internal interface IHandler
    {
        int Id { get; }

        bool TryHandle(object message);
    }
}

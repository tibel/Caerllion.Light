namespace Caerllion.Light
{
    internal sealed class RemoveHandlerMessage
    {
        public RemoveHandlerMessage(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }
}

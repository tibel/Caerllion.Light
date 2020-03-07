using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class InvokeMethodMessage<TRequest, TReply> : ICompletableMessage
    {
        public InvokeMethodMessage(TRequest request)
        {
            Request = request;
            ReplySource = new TaskCompletionSource<TReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public TRequest Request { get; }

        public TaskCompletionSource<TReply> ReplySource { get; }

        public bool IsHandled { get; private set; }

        public void BeginExecute()
        {
            IsHandled = true;
        }

        void ICompletableMessage.OnMessageNotHandled()
        {
            ReplySource.TrySetCanceled();
        }
    }
}

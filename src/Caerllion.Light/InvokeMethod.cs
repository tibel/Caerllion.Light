using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class InvokeMethod<TRequest, TReply> : IInvokeMethod
        where TRequest : IRequest<TReply>
    {
        public InvokeMethod(TRequest request)
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

        void IInvokeMethod.EnsureTaskCompleted()
        {
            if (!IsHandled)
                ReplySource.TrySetCanceled();
        }
    }

    internal interface IInvokeMethod
    {
        void EnsureTaskCompleted();
    }
}

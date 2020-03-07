using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class InvokeMethodMessage<TRequest, TReply> : ICompletableMessage, IHandleOnceMessage
    {
        public InvokeMethodMessage(TRequest request)
        {
            Request = request;
            ReplySource = new TaskCompletionSource<TReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public TRequest Request { get; }

        public TaskCompletionSource<TReply> ReplySource { get; }

        private bool _isHandled;

        public bool TryBeginHandle()
        {
            if (_isHandled)
            {
                return false;
            }
            else
            {
                _isHandled = true;
                return true;
            }
        }

        void ICompletableMessage.OnMessageNotHandled()
        {
            ReplySource.TrySetCanceled();
        }
    }
}

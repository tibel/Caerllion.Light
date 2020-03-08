using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal class InvokeMessage<TReply> : ICompletableMessage
    {
        private readonly TaskCompletionSource<TReply> _replySource = new TaskCompletionSource<TReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _handled;

        public Task<TReply> Completion => _replySource.Task;

        public void SetCanceled() => _replySource.TrySetCanceled();

        public void SetException(Exception ex) => _replySource.TrySetException(ex);

        public void SetResult(TReply result) => _replySource.TrySetResult(result);

        public bool TryBeginHandle() => Interlocked.Increment(ref _handled) == 1;

        void ICompletableMessage.OnMessageNotHandled() => SetCanceled();
    }

    internal sealed class InvokeMessage<TRequest, TReply> : InvokeMessage<TReply>
    {
        public InvokeMessage(TRequest request)
        {
            Request = request;
        }

        public TRequest Request { get; }
    }
}

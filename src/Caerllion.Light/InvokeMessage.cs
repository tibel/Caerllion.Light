using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal abstract class InvokeMessage
    {
        private int _handled;

        public bool TryBeginHandle() => Interlocked.Increment(ref _handled) == 1;

        public abstract void SetCanceled();

        public abstract void SetException(Exception ex);
    }

    internal abstract class InvokeMessage<TReply> : InvokeMessage
    {
        private readonly TaskCompletionSource<TReply> _replySource = new TaskCompletionSource<TReply>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TReply> Completion => _replySource.Task;

        public sealed override void SetCanceled() => _replySource.TrySetCanceled();

        public sealed override void SetException(Exception ex) => _replySource.TrySetException(ex);

        public void SetResult(TReply result) => _replySource.TrySetResult(result);
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

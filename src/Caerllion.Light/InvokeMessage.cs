using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caerllion.Light;

internal abstract class InvokeMessage
{
    private int _handled;

    public bool TryBeginHandle() => Interlocked.Increment(ref _handled) == 1;

    public abstract CancellationToken CancellationToken { get; }

    public abstract void SetCanceled();

    public abstract void SetException(Exception ex);
}

internal sealed class InvokeMessage<TRequest, TReply> : InvokeMessage
{
    private readonly TaskCompletionSource<TReply> _replySource = new TaskCompletionSource<TReply>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _cancellationRegistration;

    public InvokeMessage(TRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        CancellationToken = cancellationToken;
        if (cancellationToken.IsCancellationRequested)
        {
            SetCanceled();
        }
        else
        {
            _cancellationRegistration = cancellationToken.Register(
                static state => ((InvokeMessage<TRequest, TReply>)state!).SetCanceled(),
                this,
                useSynchronizationContext: false);

            if (cancellationToken.IsCancellationRequested)
                SetCanceled();
        }
    }

    public TRequest Request { get; }

    public Task<TReply> Completion => _replySource.Task;

    public override CancellationToken CancellationToken { get; }

    public override void SetCanceled()
    {
        _replySource.TrySetCanceled();
        _cancellationRegistration.Dispose();
    }

    public override void SetException(Exception ex)
    {
        _replySource.TrySetException(ex);
        _cancellationRegistration.Dispose();
    }

    public void SetResult(TReply result)
    {
        _replySource.TrySetResult(result);
        _cancellationRegistration.Dispose();
    }
}

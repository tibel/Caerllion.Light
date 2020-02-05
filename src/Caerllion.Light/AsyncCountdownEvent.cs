using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    public sealed class AsyncCountdownEvent
    {
        private readonly TaskCompletionSource<object> _tcs;
        private int _remainingCount;

        public AsyncCountdownEvent(int initialCount)
        {
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCount));

            _tcs = new TaskCompletionSource<object>();
            _remainingCount = initialCount;

            if (initialCount == 0)
                _tcs.TrySetResult(null);
        }

        public Task WaitAsync()
        {
            return _tcs.Task;
        }

        public void Increment()
        {
            var newCount = Interlocked.Increment(ref _remainingCount);
            if (newCount == 0)
                _tcs.TrySetResult(null);
            else if (newCount < 0 || _tcs.Task.IsCompleted)
                throw new InvalidOperationException();
        }

        public void Decrement()
        {
            var newCount = Interlocked.Decrement(ref _remainingCount);
            if (newCount == 0)
                _tcs.TrySetResult(null);
            else if (newCount < 0)
                throw new InvalidOperationException();
        }
    }
}

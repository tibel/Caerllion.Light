using System;
using System.Threading;
using System.Threading.Tasks;

namespace Caerllion.Light;

/// <summary>Provides an awaitable countdown synchronization primitive.</summary>
public sealed class AsyncCountdownEvent
{
    private readonly Lock _sync = new();
    private readonly TaskCompletionSource _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _remainingCount;

    /// <summary>Creates a countdown with the specified initial count.</summary>
    public AsyncCountdownEvent(int initialCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);

        _remainingCount = initialCount;

        if (initialCount == 0)
            _tcs.TrySetResult();
    }

    /// <summary>Returns a task that completes when the count reaches zero.</summary>
    public Task WaitAsync()
    {
        return _tcs.Task;
    }

    /// <summary>Increments the remaining count.</summary>
    public void Increment()
    {
        lock (_sync)
        {
            if (_tcs.Task.IsCompleted)
                throw new InvalidOperationException();

            if (_remainingCount == int.MaxValue)
                throw new InvalidOperationException();

            _remainingCount++;
        }
    }

    /// <summary>Decrements the remaining count.</summary>
    public void Decrement()
    {
        lock (_sync)
        {
            if (_remainingCount == 0)
                throw new InvalidOperationException();

            _remainingCount--;
            if (_remainingCount == 0)
                _tcs.TrySetResult();
        }
    }
}

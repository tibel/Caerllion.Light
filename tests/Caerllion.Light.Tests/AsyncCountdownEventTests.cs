using System;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace Caerllion.Light.Tests;

public sealed class AsyncCountdownEventTests
{
    [Test]
    public async Task ZeroCountIsImmediatelyComplete()
    {
        var countdown = new AsyncCountdownEvent(0);

        await Assert.That(countdown.WaitAsync().IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task DecrementCompletesAtZero()
    {
        var countdown = new AsyncCountdownEvent(2);
        countdown.Decrement();

        await Assert.That(countdown.WaitAsync().IsCompleted).IsFalse();

        countdown.Decrement();
        await countdown.WaitAsync();

        await Assert.That(countdown.WaitAsync().IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task IncrementThenDecrementCompletes()
    {
        var countdown = new AsyncCountdownEvent(1);
        countdown.Increment();
        countdown.Decrement();

        await Assert.That(countdown.WaitAsync().IsCompleted).IsFalse();

        countdown.Decrement();
        await countdown.WaitAsync();
    }

    [Test]
    public async Task NegativeInitialCountIsRejected()
    {
        await Assert.That(() => new AsyncCountdownEvent(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DecrementBelowZeroIsRejected()
    {
        var countdown = new AsyncCountdownEvent(1);
        countdown.Decrement();

        await Assert.That(() => countdown.Decrement()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task IncrementAfterCompletionIsRejected()
    {
        var countdown = new AsyncCountdownEvent(0);

        await Assert.That(() => countdown.Increment()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task IncrementAtMaximumCountIsRejected()
    {
        var countdown = new AsyncCountdownEvent(int.MaxValue);

        await Assert.That(() => countdown.Increment()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConcurrentIncrementsAndDecrementsCompleteExactlyOnce()
    {
        const int operationCount = 1000;
        var countdown = new AsyncCountdownEvent(1);

        await Task.Run(() => Parallel.For(0, operationCount, _ => countdown.Increment()));
        await Task.Run(() => Parallel.For(0, operationCount + 1, _ => countdown.Decrement()));
        await countdown.WaitAsync();

        await Assert.That(countdown.WaitAsync().IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task ConcurrentFinalDecrementAndIncrementKeepStateConsistent()
    {
        const int iterationCount = 256;
        var inconsistentStates = 0;

        for (var i = 0; i < iterationCount; i++)
        {
            var countdown = new AsyncCountdownEvent(1);
            using var start = new ManualResetEventSlim();
            var incrementSucceeded = false;

            var increment = Task.Run(() =>
            {
                start.Wait();
                try
                {
                    countdown.Increment();
                    incrementSucceeded = true;
                }
                catch (InvalidOperationException)
                {
                }
            });
            var decrement = Task.Run(() =>
            {
                start.Wait();
                countdown.Decrement();
            });

            start.Set();
            await Task.WhenAll(increment, decrement);

            if (incrementSucceeded && countdown.WaitAsync().IsCompletedSuccessfully)
                inconsistentStates++;
        }

        await Assert.That(inconsistentStates).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentFinalDecrementsCompleteExactlyOnce()
    {
        var countdown = new AsyncCountdownEvent(1);
        using var barrier = new Barrier(3);
        var successfulDecrements = 0;
        var rejectedDecrements = 0;

        void Decrement()
        {
            barrier.SignalAndWait();
            try
            {
                countdown.Decrement();
                Interlocked.Increment(ref successfulDecrements);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref rejectedDecrements);
            }
        }

        Parallel.Invoke(Decrement, Decrement, () => barrier.SignalAndWait());
        await countdown.WaitAsync();

        await Assert.That(successfulDecrements).IsEqualTo(1);
        await Assert.That(rejectedDecrements).IsEqualTo(1);
        await Assert.That(countdown.WaitAsync().IsCompletedSuccessfully).IsTrue();
    }
}

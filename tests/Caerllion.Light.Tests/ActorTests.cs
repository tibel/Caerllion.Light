using System;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace Caerllion.Light.Tests;

public sealed class ActorTests
{
    [Test]
    public async Task ProcessesMessagesInFifoOrder()
    {
        var values = new System.Collections.Generic.List<int>();
        await using var actor = new Actor(
            async message =>
            {
                await Task.Yield();
                values.Add((int)message);
            },
            (_, _) => throw new InvalidOperationException("Unexpected actor failure"));

        actor.Send(1);
        actor.Send(2);
        actor.Send(3);
        await actor.CompleteAsync();

        await Assert.That(string.Join(",", values)).IsEqualTo("1,2,3");
    }

    [Test]
    public async Task SendAfterCompletionIsRejectedAndCompletionIsIdempotent()
    {
        await using var actor = new Actor(
            _ => Task.CompletedTask,
            (_, _) => { });

        await Assert.That(actor.Complete()).IsTrue();
        await Assert.That(actor.Complete()).IsFalse();
        await actor.CompleteAsync();
        await actor.CompleteAsync();

        await Assert.That(actor.Send("late")).IsFalse();
    }

    [Test]
    public async Task ProcessorFailureDoesNotStopLaterMessages()
    {
        var failures = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var actor = new Actor(
            message =>
            {
                if ((int)message == 1)
                    throw new InvalidOperationException("first");

                completed.TrySetResult();
                return Task.CompletedTask;
            },
            (_, _) => Interlocked.Increment(ref failures));

        actor.Send(1);
        actor.Send(2);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await actor.CompleteAsync();

        await Assert.That(failures).IsEqualTo(1);
    }

    [Test]
    public async Task ErrorCallbackFailureDoesNotStopLaterMessages()
    {
        var secondMessageProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var actor = new Actor(
            message =>
            {
                if ((int)message == 1)
                    throw new InvalidOperationException("processor failure");

                secondMessageProcessed.TrySetResult();
                return Task.CompletedTask;
            },
            (_, _) => throw new InvalidOperationException("error callback failure"));

        actor.Send(1);
        actor.Send(2);
        await secondMessageProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await actor.CompleteAsync();

        await Assert.That(secondMessageProcessed.Task.IsCompletedSuccessfully).IsTrue();
    }
}

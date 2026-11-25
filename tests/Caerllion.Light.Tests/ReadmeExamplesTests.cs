using System;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace Caerllion.Light.Tests;

public sealed class ReadmeExamplesTests
{
    [Test]
    public async Task ReadmeExamplesCompileAndRun()
    {
        await PublishSubscribe();
        await RequestReply();
        await CancellationAwareRequest();
        await NoResultRequest();
        await GenericHandler();
        await Actor();
        await Countdown();
    }

    private static async Task PublishSubscribe()
    {
        await using var hub = new MessageHub();
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = hub.Subscribe<OrderCreated>(order => received.TrySetResult(order.Id));

        hub.Publish(new OrderCreated(42));

        await Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo(42);
    }

    private static async Task RequestReply()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.Subscribe<AddRequest, int>(
            request => request.Left + request.Right);

        var result = await hub.InvokeAsync<AddRequest, int>(new AddRequest(2, 3));

        await Assert.That(result).IsEqualTo(5);
    }

    private static async Task CancellationAwareRequest()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.SubscribeAsync<AddRequest, int>(
            async (request, cancellationToken) =>
            {
                await Task.Delay(1, cancellationToken);
                return request.Left + request.Right;
            });

        using var source = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await hub.InvokeAsync<AddRequest, int>(
            new AddRequest(2, 3),
            source.Token);

        await Assert.That(result).IsEqualTo(5);
    }

    private static async Task NoResultRequest()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.SubscribeAsync<ClearCacheRequest, Unit>(
            async _ =>
            {
                await ClearCacheAsync();
                return Unit.Value;
            });

        await hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest());
    }

    private static async Task GenericHandler()
    {
        await using var hub = new MessageHub();
        var handled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.Subscribe(message =>
        {
            if (message is not OrderCreated order)
                return false;

            handled.TrySetResult(order.Id == 42);
            return true;
        });

        hub.Publish(new OrderCreated(42));

        await Assert.That(await handled.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
    }

    private static async Task Actor()
    {
        await using var actor = new Actor(
            message => ProcessAsync(message),
            (_, _) => { });

        actor.Send("first");
        actor.Send("second");
        await actor.CompleteAsync();
    }

    private static async Task Countdown()
    {
        var countdown = new AsyncCountdownEvent(2);

        countdown.Decrement();
        await Assert.That(countdown.WaitAsync().IsCompleted).IsFalse();

        countdown.Decrement();
        await countdown.WaitAsync();
    }

    private static Task ClearCacheAsync() => Task.CompletedTask;

    private static Task ProcessAsync(object message) => Task.CompletedTask;

    private readonly record struct OrderCreated(int Id);

    private readonly record struct AddRequest(int Left, int Right) : IRequest<int>;

    private readonly record struct ClearCacheRequest : IRequest<Unit>;
}

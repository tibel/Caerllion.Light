using System;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace Caerllion.Light.Tests;

public sealed class MessageHubTests
{
    [Test]
    public async Task PublishesTypedMessage()
    {
        await using var hub = new MessageHub();
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.Subscribe<SampleMessage>(message => source.TrySetResult(message.Value));
        hub.Publish(new SampleMessage("hello"));

        await Assert.That(await source.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo("hello");
    }

    [Test]
    public async Task AsyncHandlerCompletesBeforeHubDrains()
    {
        await using var hub = new MessageHub();
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.SubscribeAsync<SampleMessage>(async _ =>
        {
            await Task.Delay(25);
            source.TrySetResult();
        });

        hub.Publish(new SampleMessage("hello"));
        await hub.CompleteAsync();

        await Assert.That(source.Task.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task NamedAsyncSubscriptionReceivesTypedMessage()
    {
        await using var hub = new MessageHub();
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.SubscribeAsync<SampleMessage>(
            message => Task.FromResult(source.TrySetResult(message.Value)));
        hub.Publish(new SampleMessage("named async"));

        await Assert.That(await source.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo("named async");
    }

    [Test]
    public async Task DisposingSubscriptionQueuesRemovalBeforeNextPublish()
    {
        await using var hub = new MessageHub();
        var count = 0;
        var subscription = hub.Subscribe<SampleMessage>(_ => Interlocked.Increment(ref count));

        subscription.Dispose();
        hub.Publish(new SampleMessage("removed"));
        await hub.CompleteAsync();

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task SubscribeAfterCompletionThrows()
    {
        await using var hub = new MessageHub();
        await hub.CompleteAsync();

        await Assert.That(() => hub.Subscribe<SampleMessage>(_ => { }))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CompleteAsyncCancellationStopsWaitingButKeepsDraining()
    {
        await using var hub = new MessageHub();
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.SubscribeAsync<SampleMessage>(async _ =>
        {
            handlerStarted.TrySetResult();
            await releaseHandler.Task;
        });

        hub.Publish(new SampleMessage("waiting"));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cancellation = new CancellationTokenSource();
        var completion = hub.CompleteAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.That(() => completion).Throws<OperationCanceledException>();

        releaseHandler.TrySetResult();
        await hub.Completion;

        await Assert.That(hub.Publish(new SampleMessage("after drain"))).IsFalse();
    }

    [Test]
    public async Task RpcReturnsReply()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.Subscribe<AddRequest, int>(request => request.Left + request.Right);

        var reply = await hub.InvokeAsync<AddRequest, int>(new AddRequest(3, 4));

        await Assert.That(reply).IsEqualTo(7);
    }

    [Test]
    public async Task RpcPropagatesHandlerException()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.Subscribe<AddRequest, int>(
            (Func<AddRequest, int>)(_ => throw new InvalidOperationException("rpc failure")));

        var exception = await Assert.That(() => hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2)))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).IsEqualTo("rpc failure");
    }

    [Test]
    public async Task RpcObservesCancellation()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.SubscribeAsync<AddRequest, int>(
            async (request, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return request.Left + request.Right;
            });

        using var cancellation = new CancellationTokenSource();
        var replyTask = hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2), cancellation.Token);
        cancellation.Cancel();

        await Assert.That(() => replyTask).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task NamedCancellationAwareRpcSubscriptionObservesCancellation()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.SubscribeAsync<AddRequest, int>(
            async (request, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return request.Left + request.Right;
            });

        using var cancellation = new CancellationTokenSource();
        var replyTask = hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2), cancellation.Token);
        cancellation.Cancel();

        await Assert.That(() => replyTask).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task PublishAfterCompletionIsRejected()
    {
        var hub = new MessageHub();
        await hub.CompleteAsync();

        await Assert.That(hub.Publish(new SampleMessage("late"))).IsFalse();
        await hub.DisposeAsync();
    }

    [Test]
    public async Task InvokeAfterCompletionIsCanceledAndCompletionIsIdempotent()
    {
        await using var hub = new MessageHub();

        await Assert.That(hub.Complete()).IsTrue();
        await Assert.That(hub.Complete()).IsFalse();
        await hub.Completion;
        await hub.CompleteAsync();

        await Assert.That(() => hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task UnhandledMessageRaisesEvent()
    {
        await using var hub = new MessageHub();
        var source = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Unhandled += (_, args) => source.TrySetResult(args.Message);

        hub.Publish(new SampleMessage("unknown"));

        await Assert.That(await source.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsTypeOf<SampleMessage>();
    }

    [Test]
    public async Task GenericHandlerMatchesMessagesAndReportsUnmatchedOnes()
    {
        await using var hub = new MessageHub();
        var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unhandled = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.Subscribe(message =>
        {
            if (message is not SampleMessage sample)
                return false;

            handled.TrySetResult(sample.Value);
            return true;
        });
        hub.Unhandled += (_, args) => unhandled.TrySetResult(args.Message);

        hub.Publish(new SampleMessage("generic"));
        hub.Publish(new OtherMessage());

        await Assert.That(await handled.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo("generic");
        await Assert.That(await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsTypeOf<OtherMessage>();
    }

    [Test]
    public async Task AllGenericHandlersRunAndTrueSuppressesUnhandled()
    {
        await using var hub = new MessageHub();
        var firstGenericCalled = false;
        var secondGenericCalled = false;
        var unhandledRaised = false;

        using var first = hub.Subscribe(_ =>
        {
            firstGenericCalled = true;
            return true;
        });
        using var second = hub.Subscribe(_ =>
        {
            secondGenericCalled = true;
            return false;
        });
        hub.Unhandled += (_, _) => unhandledRaised = true;

        hub.Publish(new SampleMessage("broadcast"));
        await hub.CompleteAsync();

        await Assert.That(firstGenericCalled).IsTrue();
        await Assert.That(secondGenericCalled).IsTrue();
        await Assert.That(unhandledRaised).IsFalse();
    }

    [Test]
    public async Task GenericHandlerExceptionsRaiseErrorAndRemainUnhandled()
    {
        await using var hub = new MessageHub();
        var error = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unhandled = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = hub.Subscribe(_ => throw new InvalidOperationException("generic failure"));
        hub.Error += (_, args) => error.TrySetResult(args.Exception);
        hub.Unhandled += (_, args) => unhandled.TrySetResult(args.Message);

        var message = new SampleMessage("fails");
        hub.Publish(message);

        await Assert.That(await error.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsTypeOf<InvalidOperationException>();
        await Assert.That(await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsSameReferenceAs(message);
    }

    [Test]
    public async Task AsyncHandlerPreservesCancellationExceptionFromFaultedTask()
    {
        await using var hub = new MessageHub();
        var error = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new OperationCanceledException(
            "handler stopped",
            new InvalidOperationException("inner failure"),
            new CancellationToken(canceled: true));

        using var subscription = hub.SubscribeAsync<SampleMessage>(_ => Task.FromException(expected));
        hub.Error += (_, args) => error.TrySetResult(args.Exception);

        hub.Publish(new SampleMessage("cancelled"));
        await hub.CompleteAsync();

        await Assert.That(await error.Task).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task AsyncHandlerPreservesCancellationExceptionThrownSynchronously()
    {
        await using var hub = new MessageHub();
        var error = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new OperationCanceledException("handler stopped");

        using var subscription = hub.SubscribeAsync<SampleMessage>(_ => throw expected);
        hub.Error += (_, args) => error.TrySetResult(args.Exception);

        hub.Publish(new SampleMessage("cancelled"));
        await hub.CompleteAsync();

        await Assert.That(await error.Task).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task ErrorEventSubscriberExceptionDoesNotStopDispatch()
    {
        await using var hub = new MessageHub();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Error += (_, _) => throw new InvalidOperationException("event failure");

        using var subscription = hub.Subscribe<SampleMessage>(message =>
        {
            if (message.Value == "fails")
                throw new InvalidOperationException("handler failure");

            received.TrySetResult(message.Value);
        });

        hub.Publish(new SampleMessage("fails"));
        hub.Publish(new SampleMessage("continues"));
        await hub.CompleteAsync();

        await Assert.That(await received.Task).IsEqualTo("continues");
    }

    [Test]
    public async Task UnhandledEventSubscriberExceptionDoesNotStopDispatch()
    {
        await using var hub = new MessageHub();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Unhandled += (_, _) => throw new InvalidOperationException("event failure");

        using var subscription = hub.Subscribe<SampleMessage>(message => received.TrySetResult(message.Value));

        hub.Publish(new OtherMessage());
        hub.Publish(new SampleMessage("continues"));
        await hub.CompleteAsync();

        await Assert.That(await received.Task).IsEqualTo("continues");
    }

    [Test]
    public async Task AllMatchingTypedHandlersReceiveMessage()
    {
        await using var hub = new MessageHub();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        void RecordMessage()
        {
            if (Interlocked.Increment(ref count) == 2)
                received.TrySetResult();
        }

        using var first = hub.Subscribe<SampleMessage>(_ => RecordMessage());
        using var second = hub.Subscribe<SampleMessage>(_ => RecordMessage());

        hub.Publish(new SampleMessage("broadcast"));
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task RpcWithoutHandlerIsCanceled()
    {
        await using var hub = new MessageHub();

        await Assert.That(() => hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task FirstRpcHandlerOwnsRequest()
    {
        await using var hub = new MessageHub();
        var secondHandlerCalled = false;
        using var first = hub.Subscribe<AddRequest, int>(request => request.Left + request.Right);
        using var second = hub.Subscribe<AddRequest, int>((Func<AddRequest, int>)(_ =>
        {
            secondHandlerCalled = true;
            return -1;
        }));

        var reply = await hub.InvokeAsync<AddRequest, int>(new AddRequest(3, 4));

        await Assert.That(reply).IsEqualTo(7);
        await Assert.That(secondHandlerCalled).IsFalse();
    }

    [Test]
    public async Task AsyncRpcPropagatesHandlerException()
    {
        await using var hub = new MessageHub();
        Func<AddRequest, Task<int>> handler = _ =>
            Task.FromException<int>(new InvalidOperationException("async rpc failure"));
        using var subscription = hub.SubscribeAsync<AddRequest, int>(handler);

        var exception = await Assert.That(() => hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2)))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).IsEqualTo("async rpc failure");
    }

    [Test]
    public async Task NamedAsyncRpcSubscriptionReturnsReply()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.SubscribeAsync<AddRequest, int>(
            request => Task.FromResult(request.Left + request.Right));

        var reply = await hub.InvokeAsync<AddRequest, int>(new AddRequest(5, 6));

        await Assert.That(reply).IsEqualTo(11);
    }

    [Test]
    public async Task NoResultRequestWaitsForSynchronousHandler()
    {
        await using var hub = new MessageHub();
        var handled = false;
        using var subscription = hub.Subscribe<ClearCacheRequest, Unit>(_ =>
        {
            handled = true;
            return Unit.Value;
        });

        await hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest());

        await Assert.That(handled).IsTrue();
    }

    [Test]
    public async Task NoResultRequestWaitsForAsynchronousHandler()
    {
        await using var hub = new MessageHub();
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = hub.SubscribeAsync<ClearCacheRequest, Unit>(async _ =>
        {
            handlerStarted.TrySetResult();
            await releaseHandler.Task;
            return Unit.Value;
        });

        var request = hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest());
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(request.IsCompleted).IsFalse();
        releaseHandler.TrySetResult();
        await request;
    }

    [Test]
    public async Task NoResultRequestPropagatesHandlerException()
    {
        await using var hub = new MessageHub();
        using var subscription = hub.Subscribe<ClearCacheRequest, Unit>(
            _ => throw new InvalidOperationException("request failure"));

        var exception = await Assert.That(() =>
                hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest()))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).IsEqualTo("request failure");
    }

    [Test]
    public async Task NoResultRequestCancellationIsPassedToHandler()
    {
        await using var hub = new MessageHub();
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = hub.SubscribeAsync<ClearCacheRequest, Unit>(
            async (_, cancellationToken) =>
            {
                handlerStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Unit.Value;
            });

        using var cancellation = new CancellationTokenSource();
        var request = hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest(), cancellation.Token);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.That(() => request).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task FirstNoResultRequestHandlerOwnsRequest()
    {
        await using var hub = new MessageHub();
        var secondHandlerCalled = false;
        using var first = hub.Subscribe<ClearCacheRequest, Unit>(_ => Unit.Value);
        using var second = hub.Subscribe<ClearCacheRequest, Unit>(_ =>
        {
            secondHandlerCalled = true;
            return Unit.Value;
        });

        await hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest());

        await Assert.That(secondHandlerCalled).IsFalse();
    }

    [Test]
    public async Task NoResultRequestWithoutHandlerIsCanceled()
    {
        await using var hub = new MessageHub();

        await Assert.That(() => hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest()))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task NoResultRequestCanceledBeforeDispatchDoesNotCallHandler()
    {
        await using var hub = new MessageHub();
        var handlerCalled = false;
        using var subscription = hub.Subscribe<ClearCacheRequest, Unit>(_ =>
        {
            handlerCalled = true;
            return Unit.Value;
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() =>
                hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest(), cancellation.Token))
            .Throws<OperationCanceledException>();
        await hub.CompleteAsync();

        await Assert.That(handlerCalled).IsFalse();
    }

    [Test]
    public async Task RequestCanceledBeforeDispatchDoesNotCallHandler()
    {
        await using var hub = new MessageHub();
        var handlerCalled = false;
        using var subscription = hub.Subscribe<AddRequest, int>((Func<AddRequest, int>)(_ =>
        {
            handlerCalled = true;
            return 3;
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => hub.InvokeAsync<AddRequest, int>(new AddRequest(1, 2), cancellation.Token))
            .Throws<OperationCanceledException>();
        await hub.CompleteAsync();

        await Assert.That(handlerCalled).IsFalse();
    }

    private sealed record SampleMessage(string Value);
    private sealed record OtherMessage;

    private readonly record struct ClearCacheRequest : IRequest<Unit>;

    private readonly record struct AddRequest(int Left, int Right) : IRequest<int>;
}

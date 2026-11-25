# Caerllion.Light

Caerllion.Light is a small, dependency-free, in-process message bus for .NET 10.
It combines pub-sub, typed RPC, generic handlers, actors, and async countdown
synchronization without imposing an application framework or lifecycle.

## Architecture

- `MessageHub` owns an unbounded `Channel<object>` and dispatches FIFO on one reader.
- Registrations and removals are represented as internal channel messages.
- Typed handlers, generic handlers, and RPC handlers implement `IMessageHandler`.
- Async handlers are awaited by the hub dispatch loop.
- RPC replies use `TaskCompletionSource<TReply>` and stop at the first matching handler.
- `Subscription.Dispose()` queues removal from the hub.
- `Actor` owns a separate unbounded channel and processes messages in FIFO order.
- `AsyncCountdownEvent` provides a thread-safe awaitable countdown.
- The unbounded queues provide no backpressure; sustained producer overload can grow memory without limit.

## Requirements

- .NET 10 SDK or newer
- No runtime package dependencies
- The library enables .NET trimming and Native AOT compatibility analyzers. Applications
  opt into Native AOT publishing in their executable project.
- Usage examples are exercised by the test suite.

## Publish-subscribe

```csharp
await using var hub = new MessageHub();
using var subscription = hub.Subscribe<OrderCreated>(order =>
{
    Console.WriteLine(order.Id);
});

hub.Publish(new OrderCreated(42));
```

Use `SubscribeAsync<TMessage>` for async typed handlers.
Disposing a subscription queues its removal.

## RPC

Implement `IRequest<TReply>` on the request type:

```csharp
public readonly record struct AddRequest(int Left, int Right) : IRequest<int>;

await using var hub = new MessageHub();
using var subscription = hub.Subscribe<AddRequest, int>(
    request => request.Left + request.Right);

int result = await hub.InvokeAsync<AddRequest, int>(new AddRequest(2, 3));
```

Use a cancellation-aware handler when the operation supports cancellation:

```csharp
using var subscription = hub.SubscribeAsync<AddRequest, int>(
    async (request, cancellationToken) =>
    {
        await Task.Delay(100, cancellationToken);
        return request.Left + request.Right;
    });

using var source = new CancellationTokenSource(TimeSpan.FromSeconds(5));
int result = await hub.InvokeAsync<AddRequest, int>(
    new AddRequest(2, 3),
    source.Token);
```

An unhandled RPC request is canceled. A matching handler can return a reply,
fault the reply task, or observe request cancellation. A request canceled before
dispatch does not invoke a handler. The first matching RPC handler to begin
handling a request owns that request.

## No-result requests

Use `Unit` as the reply type when a request has no result. This uses the same
subscription and invocation APIs as request-reply RPC while still allowing the
caller to await completion and observe exceptions or cancellation.

```csharp
public readonly record struct ClearCacheRequest : IRequest<Unit>;

await using var hub = new MessageHub();
using var subscription = hub.SubscribeAsync<ClearCacheRequest, Unit>(
    async _ =>
    {
        await ClearCacheAsync();
        return Unit.Value;
    });

await hub.InvokeAsync<ClearCacheRequest, Unit>(new ClearCacheRequest());
```

An empty request type represents an operation with no input parameters. Use
`Publish` instead when the caller does not need to await processing.

## Generic handlers

```csharp
using var subscription = hub.Subscribe(message =>
{
    if (message is OrderCreated order)
    {
        Console.WriteLine(order.Id);
        return true;
    }

    return false;
});
```

Generic handlers receive every object. Returning `true` marks the message
handled and suppresses `MessageHub.Unhandled`, but does not stop other handlers.

## Errors and unhandled messages

Typed, async, and generic handler exceptions raise `MessageHub.Error`.
Handlers that do not match do not mark a message handled.
Published messages with no handler raise `MessageHub.Unhandled`.

Exceptions thrown by event subscribers are not rethrown into the dispatch loop.

## Actor

```csharp
await using var actor = new Actor(
    async message =>
    {
        await ProcessAsync(message);
    },
    (message, exception) =>
    {
        Console.WriteLine($"Failed {message}: {exception.Message}");
    });

actor.Send("first");
actor.Send("second");
```

Actor processing is serialized and FIFO. Processor failures are reported through
the error callback and do not stop later messages.

## Completion and disposal

`MessageHub.Complete()` and `Actor.Complete()` stop accepting new writes and drain
queued work. `CompleteAsync()` also waits for the processing loop to finish.
Canceling a `CompleteAsync()` wait does not cancel draining; completion has already
started. Disposing a `Subscription` queues its removal, so messages already ahead
of that removal may still be delivered. Disposal of the hub and actor performs
the same completion and drain.

After completion, `Publish()` and `Send()` return `false`.
Subscribing to a completed hub throws `InvalidOperationException`.

## Countdown

```csharp
var countdown = new AsyncCountdownEvent(2);

countdown.Decrement(); // still waiting at a count of one

countdown.Decrement();
await countdown.WaitAsync();
```

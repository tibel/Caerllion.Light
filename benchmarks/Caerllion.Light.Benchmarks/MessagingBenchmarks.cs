using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Caerllion.Light;

namespace Caerllion.Light.Benchmarks;

public sealed class BenchmarkMessage
{
}

public readonly record struct BenchmarkRequest(int Value) : IRequest<int>;

[MemoryDiagnoser]
public class TypedDispatchBenchmarks
{
    private const int BatchSize = 512;
    private readonly BenchmarkMessage _message = new();
    private TaskCompletionSource _batchCompleted = null!;
    private MessageHub _hub = null!;
    private int _handled;

    [Params(1, 4, 16)]
    public int HandlerCount { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _hub = new MessageHub();
        _batchCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _handled = 0;

        for (var i = 0; i < HandlerCount; i++)
            _hub.Subscribe<BenchmarkMessage>(HandleMessage);
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task PublishToSynchronousHandlers()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            if (!_hub.Publish(_message))
                throw new InvalidOperationException("The message hub stopped accepting messages.");
        }

        await _batchCompleted.Task.ConfigureAwait(false);
    }

    [IterationCleanup]
    public void Cleanup() => _hub.CompleteAsync().GetAwaiter().GetResult();

    private void HandleMessage(BenchmarkMessage _)
    {
        if (++_handled == BatchSize * HandlerCount)
            _batchCompleted.TrySetResult();
    }
}

[MemoryDiagnoser]
public class AsyncDispatchBenchmarks
{
    private const int BatchSize = 512;
    private readonly BenchmarkMessage _message = new();
    private TaskCompletionSource _batchCompleted = null!;
    private MessageHub _hub = null!;
    private int _handled;

    [Params(1, 4, 16)]
    public int HandlerCount { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _hub = new MessageHub();
        _batchCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _handled = 0;

        for (var i = 0; i < HandlerCount; i++)
            _hub.SubscribeAsync<BenchmarkMessage>(HandleMessageAsync);
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task PublishToAsynchronousHandlers()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            if (!_hub.Publish(_message))
                throw new InvalidOperationException("The message hub stopped accepting messages.");
        }

        await _batchCompleted.Task.ConfigureAwait(false);
    }

    [IterationCleanup]
    public void Cleanup() => _hub.CompleteAsync().GetAwaiter().GetResult();

    private Task HandleMessageAsync(BenchmarkMessage _)
    {
        if (++_handled == BatchSize * HandlerCount)
            _batchCompleted.TrySetResult();

        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
public class RpcBenchmarks
{
    private const int BatchSize = 512;
    private readonly BenchmarkRequest _request = new(42);
    private readonly Task[] _pending = new Task[BatchSize];
    private MessageHub _hub = null!;

    [IterationSetup]
    public void Setup()
    {
        _hub = new MessageHub();
        _hub.Subscribe<BenchmarkRequest, int>(static request => request.Value + 1);
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task InvokeRpcBatch()
    {
        for (var i = 0; i < BatchSize; i++)
            _pending[i] = _hub.InvokeAsync<BenchmarkRequest, int>(_request);

        await Task.WhenAll(_pending).ConfigureAwait(false);
    }

    [IterationCleanup]
    public void Cleanup() => _hub.CompleteAsync().GetAwaiter().GetResult();
}

[MemoryDiagnoser]
public class ActorBenchmarks
{
    private const int BatchSize = 512;
    private readonly BenchmarkMessage _message = new();
    private Actor _actor = null!;
    private TaskCompletionSource _batchCompleted = null!;
    private int _processed;

    [IterationSetup]
    public void Setup()
    {
        _batchCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _processed = 0;
        _actor = new Actor(ProcessMessage, static (_, _) => { });
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task SendActorBatch()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            if (!_actor.Send(_message))
                throw new InvalidOperationException("The actor stopped accepting messages.");
        }

        await _batchCompleted.Task.ConfigureAwait(false);
    }

    [IterationCleanup]
    public void Cleanup() => _actor.CompleteAsync().GetAwaiter().GetResult();

    private Task ProcessMessage(object _)
    {
        if (++_processed == BatchSize)
            _batchCompleted.TrySetResult();

        return Task.CompletedTask;
    }
}

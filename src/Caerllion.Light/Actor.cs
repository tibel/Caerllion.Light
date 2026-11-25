using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Caerllion.Light;

/// <summary>
/// Processes objects in FIFO order through an independent task-serialized channel.
/// </summary>
public sealed class Actor : IAsyncDisposable
{
    private readonly Channel<object> _channel = Channel.CreateUnbounded<object>(
        new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly Func<object, Task> _processor;
    private readonly Action<object, Exception> _onError;
    private readonly Task _completion;

    /// <summary>Creates an actor with a task-based processor and error callback.</summary>
    public Actor(Func<object, Task> processor, Action<object, Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(onError);

        _processor = processor;
        _onError = onError;
        _completion = ProcessAsync();
    }

    /// <summary>Gets a task that completes when the actor channel has drained.</summary>
    public Task Completion => _completion;

    /// <summary>Stops accepting actor messages and drains queued messages.</summary>
    public bool Complete() => _channel.Writer.TryComplete();

    /// <summary>Completes the actor and waits for queued messages to finish processing.</summary>
    /// <remarks>Cancellation stops waiting; it does not stop the actor from draining its queue.</remarks>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        Complete();
        await _completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Queues an object for processing.</summary>
    public bool Send(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _channel.Writer.TryWrite(message);
    }

    private async Task ProcessAsync()
    {
        await foreach (var message in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await _processor(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    _onError(message, ex);
                }
                catch
                {
                    // Error subscribers must not stop actor processing.
                }
            }
        }
    }

    /// <summary>Completes and drains the actor.</summary>
    public async ValueTask DisposeAsync()
    {
        await CompleteAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

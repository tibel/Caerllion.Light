using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    public sealed class Actor<T>
    {
        private readonly Channel<T> _channel = Channel.CreateUnbounded<T>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });

        private readonly Func<T, Task> _processor;
        private readonly Action<Exception> _onError;

        public Actor(Func<T, Task> processor, Action<Exception> onError)
        {
            _processor = processor;
            _onError = onError;

            Process();
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
        }

        public void Send(T message)
        {
            _channel.Writer.TryWrite(message);
        }

        private async void Process()
        {
            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var message))
                {
                    try
                    {
                        await _processor(message).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _onError(ex);
                    }
                }
            }
        }
    }
}

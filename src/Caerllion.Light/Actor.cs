using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    public sealed class Actor
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

        public Actor(Func<object, Task> processor, Action<object, Exception> onError)
        {
            _processor = processor;
            _onError = onError;

            Process();
        }

        public bool Complete() => _channel.Writer.TryComplete();

        public bool Send(object message) => _channel.Writer.TryWrite(message);

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
                        _onError(message, ex);
                    }
                }
            }
        }
    }
}

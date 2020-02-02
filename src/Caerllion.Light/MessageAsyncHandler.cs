using System;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class MessageAsyncHandler<TMessage> : IHandler
    {
        private readonly Func<TMessage, Task> _handler;
        private readonly Action<Exception> _onError;

        public MessageAsyncHandler(int id, Func<TMessage, Task> handler, Action<Exception> onError)
        {
            Id = id;
            _handler = handler;
            _onError = onError;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            return message is TMessage m && Handle(m);
        }

        private bool Handle(TMessage message)
        {
            try
            {
                _handler(message)
                    .ContinueWith((t, s) =>
                    {
                        var onError = (Action<Exception>)s;
                        if (t.IsCanceled)
                            _onError(new OperationCanceledException());
                        else if (t.IsFaulted)
                            _onError(t.Exception.InnerException);
                    }, _onError, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnRanToCompletion);
            }
            catch (Exception ex)
            {
                _onError(ex);
            }

            return true;
        }
    }
}

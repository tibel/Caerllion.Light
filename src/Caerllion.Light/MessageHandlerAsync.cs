using System;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class MessageHandlerAsync<TMessage> : IMessageHandler
    {
        private readonly Func<TMessage, Task> _handler;
        private readonly Action<object, Exception> _onError;

        public MessageHandlerAsync(int id, Func<TMessage, Task> handler, Action<object, Exception> onError)
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
                    .ContinueWith(OnCompleted, message, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnRanToCompletion);
            }
            catch (Exception ex)
            {
                _onError(message, ex);
            }

            return true;
        }

        private void OnCompleted(Task t, object message)
        {
            if (t.IsCanceled)
                _onError(message, new OperationCanceledException());
            else if (t.IsFaulted)
                _onError(message, t.Exception.InnerException);
        }
    }
}

using System;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class InvokeMessageHandlerAsync<TRequest, TReply> : IMessageHandler
    {
        private readonly Func<TRequest, Task<TReply>> _handler;

        public InvokeMessageHandlerAsync(int id, Func<TRequest, Task<TReply>> handler)
        {
            Id = id;
            _handler = handler;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            return message is InvokeMessage<TRequest, TReply> m && Handle(m);
        }

        private bool Handle(InvokeMessage<TRequest, TReply> message)
        {
            if (!message.TryBeginHandle())
                return false;

            try
            {
                _handler.Invoke(message.Request)
                    .ContinueWith(OnCompleted, message, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (OperationCanceledException)
            {
                message.SetCanceled();
            }
            catch (Exception ex)
            {
                message.SetException(ex);
            }

            return true;
        }

        private void OnCompleted(Task<TReply> t, object state)
        {
            var message = (InvokeMessage<TRequest, TReply>)state;

            if (t.IsCanceled)
                message.SetCanceled();
            else if (t.IsFaulted)
                message.SetException(t.Exception.InnerException);
            else
                message.SetResult(t.Result);
        }
    }
}

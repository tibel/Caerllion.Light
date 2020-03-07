using System;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class InvokeMethodHandlerAsync<TRequest, TReply> : IMessageHandler
    {
        private readonly Func<TRequest, Task<TReply>> _handler;

        public InvokeMethodHandlerAsync(int id, Func<TRequest, Task<TReply>> handler)
        {
            Id = id;
            _handler = handler;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            return message is InvokeMethodMessage<TRequest, TReply> im && im.TryBeginHandle() && Handle(im);
        }

        private bool Handle(InvokeMethodMessage<TRequest, TReply> message)
        {
            try
            {
                _handler.Invoke(message.Request)
                    .ContinueWith((t, s) =>
                    {
                        var tcs = (TaskCompletionSource<TReply>)s;
                        if (t.IsCanceled)
                            tcs.TrySetCanceled();
                        else if (t.IsFaulted)
                            tcs.TrySetException(t.Exception.InnerException);
                        else
                            tcs.TrySetResult(t.Result);
                    }, message.ReplySource, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (OperationCanceledException)
            {
                message.ReplySource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                message.ReplySource.TrySetException(ex);
            }

            return true;
        }
    }
}

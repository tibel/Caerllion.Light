using System;
using System.Threading.Tasks;

namespace Caerllion.Light
{
    internal sealed class InvokeMethodAsyncHandler<TRequest, TReply> : IHandler
        where TRequest : IRequest<TReply>
    {
        private readonly Func<TRequest, Task<TReply>> _handler;

        public InvokeMethodAsyncHandler(int id, Func<TRequest, Task<TReply>> handler)
        {
            Id = id;
            _handler = handler;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            return message is InvokeMethod<TRequest, TReply> im && !im.IsHandled && Handle(im);
        }

        private bool Handle(InvokeMethod<TRequest, TReply> message)
        {
            message.BeginExecute();

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
            catch (Exception ex)
            {
                message.ReplySource.TrySetException(ex);
            }

            return true;
        }
    }
}

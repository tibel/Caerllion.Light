using System;

namespace Caerllion.Light
{
    internal sealed class InvokeMethodHandler<TRequest, TReply> : IMessageHandler
        where TRequest : IRequest<TReply>
    {
        private readonly Func<TRequest, TReply> _handler;

        public InvokeMethodHandler(int id, Func<TRequest, TReply> handler)
        {
            Id = id;
            _handler = handler;
        }

        public int Id { get; }

        public bool TryHandle(object message)
        {
            return message is InvokeMethodMessage<TRequest, TReply> im && !im.IsHandled && Handle(im);
        }

        private bool Handle(object message)
        {
            var invokeMethod = (InvokeMethodMessage<TRequest, TReply>)message;
            invokeMethod.BeginExecute();

            try
            {
                var result = _handler.Invoke(invokeMethod.Request);
                invokeMethod.ReplySource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                invokeMethod.ReplySource.TrySetException(ex);
            }

            return true;
        }
    }
}

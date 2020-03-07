﻿using System;

namespace Caerllion.Light
{
    internal sealed class InvokeMethodHandler<TRequest, TReply> : IMessageHandler
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

        private bool Handle(InvokeMethodMessage<TRequest, TReply> message)
        {
            message.BeginExecute();

            try
            {
                var result = _handler.Invoke(message.Request);
                message.ReplySource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                message.ReplySource.TrySetException(ex);
            }

            return true;
        }
    }
}
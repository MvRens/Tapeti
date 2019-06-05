using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Transient
{
    public class TransientGenericBinding : ICustomBinding
    {
        private readonly TransientRouter router;

        public TransientGenericBinding(TransientRouter router, string dynamicQueuePrefix)
        {
            this.router = router;
            DynamicQueuePrefix = dynamicQueuePrefix;
            Method = typeof(TransientRouter).GetMethod("GenericHandleResponse");
        }

        public Type Controller => typeof(TransientRouter);

        public MethodInfo Method { get; }

        public QueueBindingMode QueueBindingMode => QueueBindingMode.DirectToQueue;

        public string StaticQueueName => null;

        public string DynamicQueuePrefix { get; }

        public Type MessageClass => null;

        public bool Accept(Type messageClass)
        {
            return true;
        }

        public bool Accept(IMessageContext context, object message)
        {
            return true;
        }

        public Task Invoke(IMessageContext context, object message)
        {
            router.GenericHandleResponse(message, context);
            return Task.CompletedTask;
        }

        public void SetQueueName(string queueName)
        {
            router.TransientResponseQueueName = queueName;
        }
    }
}
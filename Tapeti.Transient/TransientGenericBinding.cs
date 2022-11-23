using System;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Transient
{
    /// <summary>
    /// Implements a binding for transient request response messages.
    /// Register this binding using the WithTransient config extension method.
    /// </summary>
    internal class TransientGenericBinding : IBinding
    {
        private readonly TransientRouter router;
        private readonly string dynamicQueuePrefix;

        /// <inheritdoc />
        public string? QueueName { get; private set; }

        /// <inheritdoc />
        public QueueType? QueueType => Config.QueueType.Dynamic;


        /// <summary>
        /// </summary>
        public TransientGenericBinding(TransientRouter router, string dynamicQueuePrefix)
        {
            this.router = router;
            this.dynamicQueuePrefix = dynamicQueuePrefix;
        }


        /// <inheritdoc />
        public async ValueTask Apply(IBindingTarget target)
        {
            QueueName = await target.BindDynamicDirect(dynamicQueuePrefix, null);
            router.TransientResponseQueueName = QueueName;
        }


        /// <inheritdoc />
        public bool Accept(Type messageClass)
        {
            return true;
        }


        /// <inheritdoc />
        public ValueTask Invoke(IMessageContext context)
        {
            router.HandleMessage(context);
            return default;
        }


        /// <inheritdoc />
        public ValueTask Cleanup(IMessageContext context, ConsumeResult consumeResult)
        {
            return default;
        }
    }
}
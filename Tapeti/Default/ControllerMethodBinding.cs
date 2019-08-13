using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Binding implementation for controller methods. Do not instantiate this class yourself,
    /// instead use the ITapetiConfigBuilder RegisterController / RegisterAllControllers extension
    /// methods.
    /// </summary>
    public class ControllerMethodBinding : IBinding
    {
        private readonly Type controller;
        private readonly MethodInfo method;
        private readonly QueueInfo queueInfo;


        /// <inheritdoc />
        public string QueueName { get; private set; }


        /// <inheritdoc />
        public ControllerMethodBinding(Type controller, MethodInfo method, QueueInfo queueInfo)
        {
            this.controller = controller;
            this.method = method;
            this.queueInfo = queueInfo;
        }


        /// <inheritdoc />
        public Task Apply(IBindingTarget target)
        {
            // TODO ControllerMethodBinding
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public bool Accept(Type messageClass)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task Invoke(IMessageContext context)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        public class QueueInfo
        {
            /// <summary>
            /// Whether the queue is dynamic or durable.
            /// </summary>
            public bool Dynamic { get; set; }

            /// <summary>
            /// The name of the durable queue, or optional prefix of the dynamic queue.
            /// </summary>
            public string Name { get; set; }


            /// <summary>
            /// Determines if the QueueInfo properties contain a valid combination.
            /// </summary>
            public bool IsValid => Dynamic|| !string.IsNullOrEmpty(Name);
        }
    }
}

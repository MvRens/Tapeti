using System.Reflection;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    /// <summary>
    /// Default implementation for IFlowHandlerContext
    /// </summary>
    internal class FlowHandlerContext : IFlowHandlerContext
    {
        /// <summary>
        /// </summary>
        public FlowHandlerContext(ITapetiConfig config, object? controller, MethodInfo method)
        {
            Config = config;
            Controller = controller;
            Method = method;
        }


        /// <summary>
        /// </summary>
        public FlowHandlerContext(IMessageContext source)
        {
            var controllerPayload = source.Get<ControllerMessageContextPayload>();

            Config = source.Config;
            Controller = controllerPayload.Controller;
            Method = controllerPayload.Binding.Method;
            MessageContext = source;
        }


        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public ITapetiConfig Config { get; }

        /// <inheritdoc />
        public object? Controller { get; }

        /// <inheritdoc />
        public MethodInfo Method { get; }

        /// <inheritdoc />
        public IMessageContext? MessageContext { get; }
    }
}

using System.Reflection;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation for IFlowHandlerContext
    /// </summary>
    internal class FlowHandlerContext : IFlowHandlerContext
    {
        /// <summary>
        /// </summary>
        public FlowHandlerContext()
        {
        }


        /// <summary>
        /// </summary>
        public FlowHandlerContext(IMessageContext source)
        {
            if (source == null)
                return;

            if (!source.TryGet<ControllerMessageContextPayload>(out var controllerPayload))
                return;

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
        public ITapetiConfig Config { get; set; }

        /// <inheritdoc />
        public object Controller { get; set; }

        /// <inheritdoc />
        public MethodInfo Method { get; set; }

        /// <inheritdoc />
        public IMessageContext MessageContext { get; set; }
    }
}

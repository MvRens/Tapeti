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
        /// <inheritdoc />
        public FlowHandlerContext()
        {
        }


        /// <inheritdoc />
        public FlowHandlerContext(IControllerMessageContext source)
        {
            if (source == null)
                return;

            Config = source.Config;
            Controller = source.Controller;
            Method = source.Binding.Method;
            ControllerMessageContext = source;
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
        public IControllerMessageContext ControllerMessageContext { get; set; }
    }
}

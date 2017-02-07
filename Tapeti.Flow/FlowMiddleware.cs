using System.Collections.Generic;
using Tapeti.Config;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    public class FlowMiddleware : IMiddlewareBundle
    {
        public IEnumerable<object> GetContents(IDependencyResolver dependencyResolver)
        {
            var container = dependencyResolver as IDependencyContainer;

            // ReSharper disable once InvertIf
            if (container != null)
            {
                container.RegisterDefault<IFlowProvider, FlowProvider>();
                container.RegisterDefault<IFlowHandler, FlowProvider>();
                container.RegisterDefault<IFlowRepository, NonPersistentFlowRepository>();
                container.RegisterDefault<IFlowStore, FlowStore>();
            }

            return new[] { new FlowBindingMiddleware() };
        }
    }
}

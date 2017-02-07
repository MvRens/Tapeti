using System.Collections.Generic;
using Tapeti.Config;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    public class FlowMiddleware : ITapetiExtension
    {
        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefault<IFlowProvider, FlowProvider>();
            container.RegisterDefault<IFlowHandler, FlowProvider>();
            container.RegisterDefault<IFlowRepository, NonPersistentFlowRepository>();
            container.RegisterDefault<IFlowStore, FlowStore>();
        }

        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return new[] { new FlowBindingMiddleware() };
        }
    }
}

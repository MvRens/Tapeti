using System.Collections.Generic;
using Tapeti.Config;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    public class FlowMiddleware : ITapetiExtension
    {
        private IFlowRepository flowRepository;

        public FlowMiddleware(IFlowRepository flowRepository)
        {
            this.flowRepository = flowRepository;
        }

        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefault<IFlowProvider, FlowProvider>();
            container.RegisterDefault<IFlowStarter, FlowStarter>();
            container.RegisterDefault<IFlowHandler, FlowProvider>();
            container.RegisterDefaultSingleton<IFlowRepository>(() => flowRepository ?? new NonPersistentFlowRepository());
            container.RegisterDefaultSingleton<IFlowStore, FlowStore>();
        }

        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            yield return new FlowBindingMiddleware();
            yield return new FlowCleanupMiddleware();
        }
    }
}

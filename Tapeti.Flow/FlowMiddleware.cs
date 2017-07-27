using System.Collections.Generic;
using Tapeti.Config;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    public class FlowMiddleware : ITapetiExtension
    {
        private IFlowRepository<Default.FlowState> flowRepository;

        public FlowMiddleware(IFlowRepository<Default.FlowState> flowRepository)
        {
            this.flowRepository = flowRepository;
        }

        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefault<IFlowProvider, FlowProvider>();
            container.RegisterDefault<IFlowStarter, FlowStarter>();
            container.RegisterDefault<IFlowHandler, FlowProvider>();
            container.RegisterDefault<IFlowRepository<FlowState>>(() => flowRepository ?? new NonPersistentFlowRepository<Default.FlowState>());
            container.RegisterDefault<IFlowStore, FlowStore>();
        }

        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return new[] { new FlowBindingMiddleware() };
        }
    }
}

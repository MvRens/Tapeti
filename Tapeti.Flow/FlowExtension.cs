using System.Collections.Generic;
using Tapeti.Config;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    /// <summary>
    /// Provides the Flow middleware.
    /// </summary>
    public class FlowExtension : ITapetiExtension
    {
        private readonly IFlowRepository flowRepository;

        /// <summary>
        /// </summary>
        public FlowExtension(IFlowRepository flowRepository)
        {
            this.flowRepository = flowRepository;
        }

        /// <inheritdoc />
        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefault<IFlowProvider, FlowProvider>();
            container.RegisterDefault<IFlowStarter, FlowStarter>();
            container.RegisterDefault<IFlowHandler, FlowProvider>();
            container.RegisterDefaultSingleton(() => flowRepository ?? new NonPersistentFlowRepository());
            container.RegisterDefaultSingleton<IFlowStore, FlowStore>();
        }

        /// <inheritdoc />
        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            yield return new FlowBindingMiddleware();
        }
    }
}

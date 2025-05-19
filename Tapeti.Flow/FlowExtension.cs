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
        private readonly IDurableFlowStore? durableFlowStore;


        /// <inheritdoc cref="FlowExtension"/>
        public FlowExtension(IDurableFlowStore? durableFlowStore)
        {
            this.durableFlowStore = durableFlowStore;
        }


        /// <inheritdoc />
        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefault<IFlowProvider, FlowProvider>();
            container.RegisterDefault<IFlowStarter, FlowStarter>();
            container.RegisterDefault<IFlowHandler, FlowProvider>();
            container.RegisterDefaultSingleton<IDynamicFlowStore>(() => new InMemoryFlowStore());
            container.RegisterDefaultSingleton(() => durableFlowStore ?? new InMemoryFlowStore());
            container.RegisterDefaultSingleton<IFlowStore, FlowStore>();
        }


        /// <inheritdoc />
        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            yield return new FlowBindingMiddleware();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Tapeti.Config;

namespace Tapeti.Transient
{
    /// <inheritdoc cref="ITapetiExtension" />
    public class TransientExtension : ITapetiExtensionBinding
    {
        private readonly string dynamicQueuePrefix;
        private readonly TransientRouter router;


        /// <summary>
        /// </summary>
        public TransientExtension(TimeSpan defaultTimeout, string dynamicQueuePrefix)
        {
            this.dynamicQueuePrefix = dynamicQueuePrefix;
            router = new TransientRouter(defaultTimeout);
        }


        /// <inheritdoc />
        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefaultSingleton(router);
            container.RegisterDefault<ITransientPublisher, TransientPublisher>();
        }


        /// <inheritdoc />
        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return Enumerable.Empty<object>();
        }


        /// <inheritdoc />
        public IEnumerable<IBinding> GetBindings(IDependencyResolver dependencyResolver)
        {
            yield return new TransientGenericBinding(router, dynamicQueuePrefix);
        }
    }
}
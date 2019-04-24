using System;
using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Transient
{
    public class TransientMiddleware : ITapetiExtension, ITapetiExtentionBinding
    {
        private string dynamicQueuePrefix;
        private TimeSpan defaultTimeout;

        public TransientMiddleware(TimeSpan defaultTimeout, string dynamicQueuePrefix)
        {
            this.dynamicQueuePrefix = dynamicQueuePrefix;
            this.defaultTimeout = defaultTimeout;
        }

        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefaultSingleton(() => new TransientRouter(container.Resolve<IInternalPublisher>(), defaultTimeout));
            container.RegisterDefault<ITransientPublisher, TransientPublisher>();
        }

        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return new object[0];
        }

        public IEnumerable<ICustomBinding> GetBindings(IDependencyResolver dependencyResolver)
        {
            yield return new TransientGenericBinding(dependencyResolver.Resolve<TransientRouter>(), dynamicQueuePrefix);
        }
    }
}
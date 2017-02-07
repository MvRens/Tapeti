using System.Collections.Generic;

namespace Tapeti.Config
{
    public interface ITapetiExtension
    {
        void RegisterDefaults(IDependencyContainer container);

        IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver);
    }
}

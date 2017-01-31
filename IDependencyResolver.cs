using System;
using Tapeti.Config;

namespace Tapeti
{
    public interface IDependencyResolver
    {
        T Resolve<T>() where T : class;
        object Resolve(Type type);
    }


    public interface IDependencyContainer : IDependencyResolver
    {
        void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService;
        void RegisterPublisher(Func<IPublisher> publisher);
        void RegisterConfig(IConfig config);
        void RegisterController(Type type);
    }
}

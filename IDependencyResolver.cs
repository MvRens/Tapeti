using System;

namespace Tapeti
{
    public interface IDependencyResolver
    {
        T Resolve<T>() where T : class;
        object Resolve(Type type);
    }


    public interface IDependencyInjector : IDependencyResolver
    {
        void RegisterDefault<TService, TImplementation>() where TService : class where TImplementation : class, TService;
        void RegisterPublisher(Func<IPublisher> publisher);
        void RegisterController(Type type);
    }
}

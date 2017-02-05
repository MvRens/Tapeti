using System;

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
        void RegisterDefault<TService>(Func<TService> factory) where TService : class;

        void RegisterDefaultSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService;
        void RegisterDefaultSingleton<TService>(TService instance) where TService : class;
        void RegisterDefaultSingleton<TService>(Func<TService> factory) where TService : class;

        void RegisterController(Type type);
    }
}

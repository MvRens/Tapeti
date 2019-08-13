using System;

namespace Tapeti
{
    /// <summary>
    /// Wrapper interface for an IoC container to allow dependency injection in Tapeti.
    /// </summary>
    public interface IDependencyResolver
    {
        T Resolve<T>() where T : class;
        object Resolve(Type type);
    }


    /// <summary>
    /// Allows registering controller classes into the IoC container. Also registers default implementations,
    /// so that the calling application may override these.
    /// </summary>
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

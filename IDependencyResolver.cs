using System;

namespace Tapeti
{
    public interface IDependencyResolver
    {
        T Resolve<T>() where T : class;
    }


    public interface IDependencyInjector : IDependencyResolver
    {
        void RegisterController(Type type);
    }
}

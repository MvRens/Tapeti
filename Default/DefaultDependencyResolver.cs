using System;

namespace Tapeti.Default
{
    /**
     * !! IoC Container 9000 !!
     * 
     * ...you probably want to replace this one as soon as possible.
     * 
     * A Simple Injector implementation is provided in the Tapeti.SimpleInjector package.
     */
    public class DefaultDependencyResolver : IDependencyInjector
    {
        private readonly Lazy<DefaultControllerFactory> controllerFactory;
        private readonly Lazy<DefaultRoutingKeyStrategy> routingKeyStrategy = new Lazy<DefaultRoutingKeyStrategy>();
        private readonly Lazy<DefaultMessageSerializer> messageSerializer = new Lazy<DefaultMessageSerializer>();



        public DefaultDependencyResolver(Func<IPublisher> publisherFactory)
        {
            controllerFactory = new Lazy<DefaultControllerFactory>(() => new DefaultControllerFactory(publisherFactory));
        }


        public T Resolve<T>() where T : class
        {
            if (typeof(T) == typeof(IControllerFactory))
                return (T)(controllerFactory.Value as IControllerFactory);

            if (typeof(T) == typeof(IRoutingKeyStrategy))
                return (T)(routingKeyStrategy.Value as IRoutingKeyStrategy);

            if (typeof(T) == typeof(IMessageSerializer))
                return (T)(messageSerializer.Value as IMessageSerializer);

            return default(T);
        }


        public void RegisterController(Type type)
        {
            controllerFactory.Value.RegisterController(type);
        }
    }
}

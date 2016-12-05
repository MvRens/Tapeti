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
        private readonly Lazy<ILogger> logger;
        private IPublisher publisher;


        public DefaultDependencyResolver()
        {
            controllerFactory = new Lazy<DefaultControllerFactory>(() => new DefaultControllerFactory(() => publisher));

            logger = new Lazy<ILogger>(() =>
            {
                // http://stackoverflow.com/questions/6408588/how-to-tell-if-there-is-a-console
                try
                {
                    // ReSharper disable once UnusedVariable
                    var dummy = Console.WindowHeight;

                    return new ConsoleLogger();
                }
                catch
                {
                    return new DevNullLogger();
                }
            });
        }


        public T Resolve<T>() where T : class
        {
            if (typeof(T) == typeof(IControllerFactory))
                return (T)(controllerFactory.Value as IControllerFactory);

            if (typeof(T) == typeof(IRoutingKeyStrategy))
                return (T)(routingKeyStrategy.Value as IRoutingKeyStrategy);

            if (typeof(T) == typeof(IMessageSerializer))
                return (T)(messageSerializer.Value as IMessageSerializer);

            if (typeof(T) == typeof(ILogger))
                return (T)logger.Value;

            return default(T);
        }


        public void RegisterPublisher(IPublisher value)
        {
            publisher = value;
        }


        public void RegisterController(Type type)
        {
            controllerFactory.Value.RegisterController(type);
        }
    }
}

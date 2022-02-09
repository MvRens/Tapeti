using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using ExampleLib;
using Ninject;
using Tapeti;
using Tapeti.Autofac;
using Tapeti.CastleWindsor;
using Tapeti.DataAnnotations;
using Tapeti.Default;
using Tapeti.Ninject;
using Tapeti.SimpleInjector;
using Tapeti.UnityContainer;
using Unity;
using Container = SimpleInjector.Container;

// ReSharper disable UnusedMember.Global

namespace _01_PublishSubscribe
{
    public class Program
    {
        public static void Main()
        {
            var dependencyResolver = GetSimpleInjectorDependencyResolver();

            // or use your IoC container of choice:
            //var dependencyResolver = GetAutofacDependencyResolver();
            //var dependencyResolver = GetCastleWindsorDependencyResolver();
            //var dependencyResolver = GetUnityDependencyResolver();
            //var dependencyResolver = GetNinjectDependencyResolver();

            // This helper is used because this example is not run as a service. You do not
            // need it in your own applications.
            var helper = new ExampleConsoleApp(dependencyResolver);
            helper.Run(MainAsync);
        }


        internal static async Task MainAsync(IDependencyResolver dependencyResolver, Func<Task> waitForDone)
        {
            var config = new TapetiConfig(dependencyResolver)
                .WithDataAnnotations()
                .RegisterAllControllers()
                .Build();

            await using var connection = new TapetiConnection(config)
            {
                // Params is optional if you want to use the defaults, but we'll set it 
                // explicitly for this example
                Params = new TapetiConnectionParams
                {
                    HostName = "localhost",
                    Username = "guest",
                    Password = "guest",

                    // These properties allow you to identify the connection in the RabbitMQ Management interface
                    ClientProperties = new Dictionary<string, string>
                    {
                        { "example", "01 - Publish Subscribe" }
                    }
                }
            };

            // IoC containers that separate the builder from the resolver (Autofac) must be built after
            // creating a TapetConnection, as it modifies the container by injecting IPublisher.
            (dependencyResolver as AutofacDependencyResolver)?.Build();


            // Create the queues and start consuming immediately.
            // If you need to do some processing before processing messages, but after the
            // queues have initialized, pass false as the startConsuming parameter and store
            // the returned ISubscriber. Then call Resume on it later.
            await connection.Subscribe();


            // We could get an IPublisher from the container directly, but since you'll usually use
            // it as an injected constructor parameter this shows
            await dependencyResolver.Resolve<ExamplePublisher>().SendTestMessage();


            // Wait for the controller to signal that the message has been received
            await waitForDone();
        }


        internal static IDependencyContainer GetSimpleInjectorDependencyResolver()
        {
            var container = new Container();

            container.Register<ILogger, ConsoleLogger>();
            container.Register<ExamplePublisher>();

            return new SimpleInjectorDependencyResolver(container);
        }


        internal static IDependencyContainer GetAutofacDependencyResolver()
        {
            var containerBuilder = new ContainerBuilder();

            containerBuilder
                .RegisterType<ConsoleLogger>()
                .As<ILogger>();

            containerBuilder
                .RegisterType<ExamplePublisher>()
                .AsSelf();

            return new AutofacDependencyResolver(containerBuilder);
        }


        internal static IDependencyContainer GetCastleWindsorDependencyResolver()
        {
            var container = new WindsorContainer();

            // This exact combination is registered by TapetiConfig when running in a console,
            // and Windsor will throw an exception for that. This is specific to the WindsorDependencyResolver as it
            // relies on the "first one wins" behaviour of Windsor and does not check the registrations.
            //
            // You can of course register another ILogger instead, like DevNullLogger.
            //container.Register(Component.For<ILogger>().ImplementedBy<ConsoleLogger>());

            container.Register(Component.For<ExamplePublisher>());

            return new WindsorDependencyResolver(container);
        }


        internal static IDependencyContainer GetUnityDependencyResolver()
        {
            var container = new UnityContainer();

            container.RegisterType<ILogger, ConsoleLogger>();
            container.RegisterType<ExamplePublisher>();

            return new UnityDependencyResolver(container);
        }


        internal static IDependencyContainer GetNinjectDependencyResolver()
        {
            var kernel = new StandardKernel();

            kernel.Bind<ILogger>().To<ConsoleLogger>();
            kernel.Bind<ExamplePublisher>().ToSelf();

            return new NinjectDependencyResolver(kernel);
        }
    }
}

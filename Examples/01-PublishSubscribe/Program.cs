using System;
using System.Threading.Tasks;
using ExampleLib;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Default;
using Tapeti.SimpleInjector;

namespace _01_PublishSubscribe
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var container = new Container();
            var dependencyResolver = new SimpleInjectorDependencyResolver(container);

            container.Register<ILogger, ConsoleLogger>();
            container.Register<ExamplePublisher>();


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

            using (var connection = new TapetiConnection(config)
            {
                // Params is optional if you want to use the defaults, but we'll set it 
                // explicitly for this example
                Params = new TapetiConnectionParams
                {
                    HostName = "localhost",
                    Username = "guest",
                    Password = "guest"
                }
            })
            {
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
        }
    }
}

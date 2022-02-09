using System;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using SimpleInjector;
using Tapeti;
using Tapeti.Default;
using Tapeti.SimpleInjector;

namespace _02_DeclareDurableQueues
{
    public class Program
    {
        public static void Main()
        {
            var container = new Container();
            var dependencyResolver = new SimpleInjectorDependencyResolver(container);

            container.Register<ILogger, ConsoleLogger>();

            var helper = new ExampleConsoleApp(dependencyResolver);
            helper.Run(MainAsync);
        }


        internal static async Task MainAsync(IDependencyResolver dependencyResolver, Func<Task> waitForDone)
        {
            var config = new TapetiConfig(dependencyResolver)
                .RegisterAllControllers()
                .EnableDeclareDurableQueues()
                .Build();

            await using var connection = new TapetiConnection(config);

            // This creates or updates the durable queue
            await connection.Subscribe();

            await dependencyResolver.Resolve<IPublisher>().Publish(new PublishSubscribeMessage
            {
                Greeting = "Hello durable queue!"
            });

            // Wait for the controller to signal that the message has been received
            await waitForDone();
        }
    }
}

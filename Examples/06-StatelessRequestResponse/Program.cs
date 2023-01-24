using System;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Default;
using Tapeti.SimpleInjector;

namespace _06_StatelessRequestResponse
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
                .WithDataAnnotations()
                .RegisterAllControllers()
                .Build();


            await using var connection = new TapetiConnection(config);
            await connection.Subscribe();

            var publisher = dependencyResolver.Resolve<IPublisher>();
            await publisher.PublishRequest<ExampleMessageController, QuoteRequestMessage, QuoteResponseMessage>(
                new QuoteRequestMessage
                {
                    Amount = 1
                }, 
                c => c.HandleQuoteResponse);

            await waitForDone();
        }
    }
}

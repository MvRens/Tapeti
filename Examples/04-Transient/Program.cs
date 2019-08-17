using System;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Default;
using Tapeti.SimpleInjector;
using Tapeti.Transient;

namespace _04_Transient
{
    public class Program
    {
        public static void Main(string[] args)
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
                .WithTransient(TimeSpan.FromSeconds(5), "tapeti.example.04.transient")
                .RegisterAllControllers()
                .Build();


            using (var connection = new TapetiConnection(config))
            {
                await connection.Subscribe();


                Console.WriteLine("Sending request...");

                var transientPublisher = dependencyResolver.Resolve<ITransientPublisher>();
                var response = await transientPublisher.RequestResponse<LoggedInUsersRequestMessage, LoggedInUsersResponseMessage>(
                    new LoggedInUsersRequestMessage());

                Console.WriteLine("Response: " + response.Count);


                // Unlike the other example, there is no need to call waitForDone, once we're here the response has been handled.
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using ExampleLib;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Default;
using Tapeti.Flow;
using Tapeti.SimpleInjector;

namespace _03_FlowRequestResponse
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
                .WithFlow()
                .RegisterAllControllers()
                .Build();


            using (var connection = new TapetiConnection(config))
            {
                // Must be called before using any flow. When using a persistent repository like the
                // SQL server implementation, you can run any required update scripts (for example, using DbUp)
                // before calling this Load method.
                // Call after creating the TapetiConnection, as it modifies the container to inject IPublisher.
                await dependencyResolver.Resolve<IFlowStore>().Load();


                // This creates or updates the durable queue
                await connection.Subscribe();


                var flowStarter = dependencyResolver.Resolve<IFlowStarter>();

                var startData = new SendingFlowController.StartData
                {
                    RequestStartTime = DateTime.Now,
                    Amount = 1
                };


                await flowStarter.Start<SendingFlowController, SendingFlowController.StartData>(c => c.StartFlow, startData);


                // Wait for the controller to signal that the message has been received
                await waitForDone();
            }
        }
    }
}

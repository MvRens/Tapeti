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
        public static void Main()
        {
            var container = new Container();
            var dependencyResolver = new SimpleInjectorDependencyResolver(container);

            container.Register<ILogger, ConsoleLogger>();

            var helper = new ExampleConsoleApp(dependencyResolver, 2);
            helper.Run(MainAsync);
        }


        internal static async Task MainAsync(IDependencyResolver dependencyResolver, Func<Task> waitForDone)
        {
            var config = new TapetiConfig(dependencyResolver)
                .WithDataAnnotations()
                .WithFlow()
                .RegisterAllControllers()
                .Build();


            await using var connection = new TapetiConnection(config);

            // Must be called before using any flow. When using a persistent repository like the
            // SQL server implementation, you can run any required update scripts (for example, using DbUp)
            // before calling this Load method.
            // Call after creating the TapetiConnection, as it modifies the container to inject IPublisher.
            await dependencyResolver.Resolve<IFlowStore>().Load();


            await connection.Subscribe();


            var flowStarter = dependencyResolver.Resolve<IFlowStarter>();

            var startData = new SimpleFlowController.StartData
            {
                RequestStartTime = DateTime.Now,
                Amount = 1
            };


            await flowStarter.Start<SimpleFlowController, SimpleFlowController.StartData>(c => c.StartFlow, startData);
            await flowStarter.Start<ParallelFlowController>(c => c.StartFlow);


            // Wait for the controller to signal that the message has been received
            await waitForDone();
        }
    }
}

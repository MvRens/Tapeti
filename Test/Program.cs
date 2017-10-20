using System;
using System.Threading.Tasks;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Flow;
using Tapeti.Flow.SQL;
using Tapeti.Helpers;
using Tapeti.SimpleInjector;
using System.Threading;

namespace Test
{
    internal class Program
    {
        private static void Main()
        {
            // TODO SQL based flow store
            // TODO logging
            // TODO uitzoeken of we consumers kunnen pauzeren (denk: SQL down) --> nee, EFDBContext Get Async maken en retryen? kan dat, of timeout dan Rabbit?

            var container = new Container();
            container.Register<MarcoEmitter>();
            container.Register<Visualizer>();
            container.Register<ILogger, Tapeti.Default.ConsoleLogger>();

            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .WithFlow()
                .WithDataAnnotations()
                .RegisterAllControllers()
                .Build();

            using (var connection = new TapetiConnection(config)
            {
                Params = new TapetiAppSettingsConnectionParams()
            })
            {
                var flowStore = container.GetInstance<IFlowStore>();
                var flowStore2 = container.GetInstance<IFlowStore>();

                Console.WriteLine("IFlowHandler is singleton = " + (flowStore == flowStore2));

                connection.Connected += (sender, e) => {
                    Console.WriteLine("Event Connected");
                };
                connection.Disconnected += (sender, e) => {
                    Console.WriteLine("Event Disconnected");
                };
                connection.Reconnected += (sender, e) => {
                    Console.WriteLine("Event Reconnected");
                };

                Console.WriteLine("Subscribing...");
                var subscriber = connection.Subscribe(false).Result;

                Console.WriteLine("Consuming...");
                subscriber.Resume().Wait();

                Console.WriteLine("Done!");

                connection.GetPublisher().Publish(new FlowEndController.PingMessage());

                container.GetInstance<IFlowStarter>().Start<MarcoController, bool>(c => c.StartFlow, true);

                Thread.Sleep(1000);

                var emitter = container.GetInstance<MarcoEmitter>();
                emitter.Run().Wait();


            }
        }
    }
}

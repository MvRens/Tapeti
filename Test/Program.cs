using System;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Flow;
using Tapeti.Flow.SQL;
using Tapeti.Helpers;
using Tapeti.SimpleInjector;

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

            //container.Register<IFlowRepository>(() => new EF(serviceID));

            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .WithFlow()
                //.WithFlowSqlRepository("data source=localhost;initial catalog=lef;integrated security=True;multipleactiveresultsets=True", 1)
                .WithDataAnnotations()
                .RegisterAllControllers()
                .Build();

            using (var connection = new TapetiConnection(config)
            {
                Params = new TapetiAppSettingsConnectionParams()
            })
            {
                Console.WriteLine("Subscribing...");
                var subscriber = connection.Subscribe(false).Result;

                Console.WriteLine("Consuming...");
                subscriber.Resume().Wait();

                Console.WriteLine("Done!");

                container.GetInstance<IFlowStarter>().Start<MarcoController>(c => c.StartFlow);

                var emitter = container.GetInstance<MarcoEmitter>();
                emitter.Run().Wait();
            }
        }
    }
}

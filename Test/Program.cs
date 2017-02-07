using System;
using SimpleInjector;
using Tapeti;
using Tapeti.Flow;
using Tapeti.SimpleInjector;

namespace Test
{
    internal class Program
    {
        private static void Main()
        {
            // TODO SQL based flow store
            // TODO logging

            var container = new Container();
            container.Register<MarcoEmitter>();
            container.Register<Visualizer>();

            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .WithFlow()
                .RegisterAllControllers()
                .Build();

            using (var connection = new TapetiConnection(config)
            {
                Params = new TapetiConnectionParams
                {
                    HostName = "localhost",
                    PrefetchCount = 200
                }
            })
            {
                Console.WriteLine("Subscribing...");
                connection.Subscribe().Wait();
                Console.WriteLine("Done!");

                var emitter = container.GetInstance<MarcoEmitter>();
                emitter.Run().Wait();
            }
        }
    }
}

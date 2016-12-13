using System;
using SimpleInjector;
using Tapeti;
using Tapeti.Saga;
using Tapeti.SimpleInjector;

namespace Test
{
    internal class Program
    {
        private static void Main()
        {
            var container = new Container();
            container.Register<MarcoEmitter>();
            container.Register<Visualizer>();
            container.Register<ISagaStore, SagaMemoryStore>();

            var config = new TapetiConfig("test", new SimpleInjectorDependencyResolver(container))
                .Use(new SagaMiddleware())
                .RegisterAllControllers()
                .Build();

            using (var connection = new TapetiConnection(config))
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

using System;
using SimpleInjector;
using Tapeti;
using Tapeti.SimpleInjector;

namespace Test
{
    internal class Program
    {
        private static void Main()
        {
            var container = new Container();

            using (var connection = new TapetiConnectionBuilder()
                .SetExchange("test")
                .SetDependencyResolver(new SimpleInjectorDependencyResolver(container))
                .SetTopology(
                    new TapetiTopologyBuilder()
                        .RegisterAllControllers()
                        .Build())
                .Build())
            {
                container.Register<MarcoEmitter>();
    
                Console.WriteLine("Subscribing...");
                connection.Subscribe().Wait();
                Console.WriteLine("Done!");

                var emitter = container.GetInstance<MarcoEmitter>();
                emitter.Run().Wait();
            }
        }
    }
}

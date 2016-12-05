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

            using (var connection = new TapetiConnection
                {
                    PublishExchange = "test",
                    SubscribeExchange = "test"
                }
                .WithDependencyResolver(new SimpleInjectorDependencyResolver(container))
                .RegisterAllControllers(typeof(Program).Assembly))
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

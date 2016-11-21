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
                container.Register(() => connection.GetPublisher());

                Console.WriteLine("Subscribing...");
                connection.Subscribe().Wait();
                Console.WriteLine("Done!");

                var publisher = connection.GetPublisher();

                //for (var x = 0; x < 5000; x++)
                while(true)
                    publisher.Publish(new MarcoMessage()).Wait();

                //Console.ReadLine();
            }
        }
    }
}

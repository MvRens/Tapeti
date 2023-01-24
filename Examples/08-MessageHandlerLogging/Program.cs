using System;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using Serilog;
using SimpleInjector;
using Tapeti;
using Tapeti.Serilog;
using Tapeti.SimpleInjector;
using ILogger = Tapeti.ILogger;

namespace _08_MessageHandlerLogging
{
    public class Program
    {
        public static void Main()
        {
            var container = new Container();
            var dependencyResolver = new SimpleInjectorDependencyResolver(container);

            var seriLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                
                // Include {Properties} or specific keys in the output template to see properties added to the diagnostic context
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
                .CreateLogger();

            container.RegisterInstance((Serilog.ILogger)seriLogger);
            container.Register<ILogger, TapetiSeriLogger.WithMessageLogging>();
            

            var helper = new ExampleConsoleApp(dependencyResolver);
            helper.Run(MainAsync);

            seriLogger.Dispose();
        }


        internal static async Task MainAsync(IDependencyResolver dependencyResolver, Func<Task> waitForDone)
        {
            var config = new TapetiConfig(dependencyResolver)
                .WithMessageHandlerLogging()
                .RegisterAllControllers()
                .Build();


            await using var connection = new TapetiConnection(config);
            var subscriber = await connection.Subscribe(false);


            var publisher = dependencyResolver.Resolve<IPublisher>();
            await publisher.Publish(new PublishSubscribeMessage
            {
                Greeting = "Hello message handler logging!"
            });
            
            await subscriber.Resume();
            await waitForDone();
        }
    }
}

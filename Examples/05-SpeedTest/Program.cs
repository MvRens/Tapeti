using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using SimpleInjector;
using Tapeti;
using Tapeti.Default;
using Tapeti.SimpleInjector;

namespace _05_SpeedTest
{
    public class Program
    {
        private const int MessageCount = 20000;

        // This does not make a massive difference, since internally Tapeti uses a single thread
        // to perform all channel operations as recommended by the RabbitMQ .NET client library.
        private const int ConcurrentTasks = 20;


        public static void Main()
        {
            var container = new Container();
            var dependencyResolver = new SimpleInjectorDependencyResolver(container);

            container.Register<ILogger, ConsoleLogger>();

            var helper = new ExampleConsoleApp(dependencyResolver);
            helper.Run(MainAsync);
        }


        internal static async Task MainAsync(IDependencyResolver dependencyResolver, Func<Task> waitForDone)
        {
            var container = (IDependencyContainer)dependencyResolver;
            container.RegisterDefaultSingleton<IMessageCounter>(new MessageCounter(MessageCount, () =>
            {
                var exampleState = dependencyResolver.Resolve<IExampleState>();
                exampleState.Done();                
            }));



            var config = new TapetiConfig(dependencyResolver)
                // On a developer test machine, this makes the difference between 2200 messages/sec and 3000 messages/sec published.
                // Interesting, but only if speed is more important than guaranteed delivery.
                //.DisablePublisherConfirms()
                .RegisterAllControllers()
                .Build();


            await using var connection = new TapetiConnection(config);
            var subscriber = await connection.Subscribe(false);


            var publisher = dependencyResolver.Resolve<IPublisher>();
            Console.WriteLine($"Publishing {MessageCount} messages...");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await PublishMessages(publisher);

            stopwatch.Stop();
            Console.WriteLine($"Took {stopwatch.ElapsedMilliseconds} ms, {MessageCount / (stopwatch.ElapsedMilliseconds / 1000F):F0} messages/sec");



            Console.WriteLine("Consuming messages...");
            await subscriber.Resume();

            stopwatch.Restart();

            await waitForDone();

            stopwatch.Stop();
            Console.WriteLine($"Took {stopwatch.ElapsedMilliseconds} ms, {MessageCount / (stopwatch.ElapsedMilliseconds / 1000F):F0} messages/sec");
        }


        internal static async Task PublishMessages(IPublisher publisher)
        {
            var semaphore = new SemaphoreSlim(ConcurrentTasks);
            var tasks = new List<Task>();

            for (var i = 0; i < MessageCount; i++)
            {
                var item = i;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await semaphore.WaitAsync();
                        await publisher.Publish(new SpeedTestMessage
                        {
                            PublishCount = item
                        });
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }
    }


    internal class MessageCounter : IMessageCounter
    {
        private readonly int max;
        private readonly Action done;
        private int count;


        public MessageCounter(int max, Action done)
        {
            this.max = max;
            this.done = done;
        }


        public void Add()
        {
            // With a prefetchcount > 1 the consumers are running in multiple threads,
            // beware of this when using singletons.
            if (Interlocked.Increment(ref count) == max)
                done();
        }
    }
}

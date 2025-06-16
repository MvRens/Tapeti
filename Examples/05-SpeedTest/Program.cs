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

        // Since Tapeti 3.4 which added supports for multiple publish channels and the accompanying task queues,
        // this affects performance a lot.
        //
        // Around 8000/sec (on a newer dev machine in Release build) for a pool size of 1,
        // up to 35000/sec for a pool of 32.
        //
        // While debugging the performance is much, MUCH slower, as in around 100/sec on the same machine.
        // Debug vs Release build does not impact the results much.
        private const int PublishChannelPoolSize = 16;


        public static void Main()
        {
            if (Debugger.IsAttached)
                Console.WriteLine("!! You are running in debug mode, performance will be severely impacted !! ");

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
                //
                // Update: note that increasing the PublishChannelPoolSize affects the performance a lot more.
                // See ConcurrentTasks.
                //
                //.DisablePublisherConfirms()
                .RegisterAllControllers()
                .Build();


            await using var connection = new TapetiConnection(config);
            connection.Params = new TapetiConnectionParams
            {
                PublishChannelPoolSize = PublishChannelPoolSize
            };
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
            var tasks = new List<Task>();

            // This is not good practise and can starve the thread pool, but good enough for a performance test
            for (var i = 0; i < MessageCount; i++)
            {
                var item = i;
                var task = Task.Run(() => publisher.Publish(new SpeedTestMessage { PublishCount = item }));

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

using System;
using System.Threading;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using SimpleInjector;
using Tapeti;
using Tapeti.Default;
using Tapeti.SimpleInjector;

namespace _07_ParallelizationTest
{
    public class Program
    {
        private const int MessageCount = 3000;
        private const int RepeatBatch = 4;


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
            var doneCount = 0;
            
            var container = (IDependencyContainer) dependencyResolver;
            container.RegisterDefaultSingleton<IMessageParallelization>(new MessageParallelization(MessageCount, () =>
            {
                doneCount++;
                Console.WriteLine($"Processed batch #{doneCount}");
                
                if (doneCount != RepeatBatch) 
                    return false;
                
                var exampleState = dependencyResolver.Resolve<IExampleState>();
                exampleState.Done();
                return true;
            }, count =>
            {
                Console.WriteLine($"Timeout while processing batch after processing {count} messages");
                
                var exampleState = dependencyResolver.Resolve<IExampleState>();
                exampleState.Done();
            }));



            var config = new TapetiConfig(dependencyResolver)
                .RegisterAllControllers()
                .Build();


            await using var connection = new TapetiConnection(config)
            {
                Params = new TapetiConnectionParams
                {
                    // Default is 50, which means we'll get a timeout after 50 messages
                    PrefetchCount = MessageCount
                }
            };
            
            var subscriber = await connection.Subscribe(false);


            var publisher = dependencyResolver.Resolve<IPublisher>();
            Console.WriteLine($"Publishing first {MessageCount} of {MessageCount * RepeatBatch} messages...");

            await PublishMessages(publisher, MessageCount);


            Console.WriteLine("Consuming messages while publishing the rest...");
            await subscriber.Resume();

            await PublishMessages(publisher, MessageCount * (RepeatBatch - 1));

            await waitForDone();
        }


        internal static async Task PublishMessages(IPublisher publisher, int messageCount)
        {
            for (var i = 0; i < messageCount; i++)
            {
                await publisher.Publish(new SpeedTestMessage
                {
                    PublishCount = i
                });
            }
        }
    }


    internal class MessageParallelization : IMessageParallelization
    {
        private readonly int max;
        private readonly Func<bool> done;
        private readonly Action<int> timeout;
        private int count;
        private readonly object waitLock = new();
        private TaskCompletionSource<bool> batchReachedTask = new();
        private Timer? messageExpectedTimer;
        private readonly TimeSpan messageExpectedTimeout = TimeSpan.FromMilliseconds(5000);


        public MessageParallelization(int max, Func<bool> done, Action<int> timeout)
        {
            this.max = max;
            this.done = done;
            this.timeout = timeout;
        }


        public Task WaitForBatch()
        {
            lock (waitLock)
            {
                if (messageExpectedTimer == null)
                    messageExpectedTimer = new Timer(_ =>
                    {
                        timeout(count);
                    }, null, messageExpectedTimeout, Timeout.InfiniteTimeSpan);
                else
                    messageExpectedTimer.Change(messageExpectedTimeout, Timeout.InfiniteTimeSpan);

                count++;
                if (count != max)
                    return batchReachedTask.Task;

                if (done())
                    messageExpectedTimer.Dispose();
                    
                count = 0;

                batchReachedTask.SetResult(true);
                batchReachedTask = new TaskCompletionSource<bool>();

                return Task.CompletedTask;
            }
        }
    }
}

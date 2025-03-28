using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Tapeti.Config.Annotations;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client.Controller
{
    public class DedicatedChannelWaitMessage
    {
    }


    public class DedicatedChannelNoWaitMessage
    {
    }


    [MessageController]
    public class DedicatedChannelController
    {
        private readonly ITestOutputHelper testOutputHelper;
        public const int WaitMessageCount = 10;
        public const int NoWaitMessageCount = 10;

        private static readonly TaskCompletionSource WaitContinue = new();
        private static readonly TaskCompletionSource WaitCompleted = new();
        private static long waitCount;

        private static readonly TaskCompletionSource NoWaitCompleted = new();
        private static long noWaitCount;


        [NoBinding]
        public static async Task WaitForNoWaitMessages()
        {
            await NoWaitCompleted.Task;
            Interlocked.Read(ref waitCount).ShouldBe(0, "NoWait messages should still be waiting");

            WaitContinue.TrySetResult();
        }

        [NoBinding]
        public static async Task WaitForWaitMessages()
        {
            await WaitCompleted.Task;
        }


        public DedicatedChannelController(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [DurableQueue("dedicated.channel.wait")]
        [DedicatedChannel]
        public async Task WaitMessage(DedicatedChannelWaitMessage message)
        {
            // To see the issue when the DedicatedChannel attribute is removed
            //testOutputHelper.WriteLine("Received wait message");

            await WaitContinue.Task;

            var count = Interlocked.Increment(ref waitCount);
            testOutputHelper.WriteLine($"Handled wait message #{count}");

            if (count == WaitMessageCount)
                WaitCompleted.TrySetResult();
        }

        
        [DurableQueue("dedicated.channel.nowait")]
        public void NoWaitMessage(DedicatedChannelNoWaitMessage message)
        {
            var count = Interlocked.Increment(ref noWaitCount);
            testOutputHelper.WriteLine($"Handled no-wait message #{count}");

            if (count == NoWaitMessageCount)
                NoWaitCompleted.TrySetResult();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Tapeti.Config.Annotations;
using Tapeti.Tests.Helpers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Tapeti.Tests.Client.Controller
{
    public class ReconnectDurableMessage
    {
        public int Number;
    }


    public class ReconnectDynamicMessage
    {
        public int Number;
    }


    [MessageController]
    public class ReconnectController
    {
        private readonly ITestOutputHelper testOutputHelper;
        private static bool durableBlock = true;
        private static readonly AsyncAutoResetEvent DurableMessageReceived = new();
        private static readonly AsyncAutoResetEvent DynamicMessageReceived = new ();


        [NoBinding]
        public static void SetBlockDurableMessage(bool block)
        {
            durableBlock = block;
        }


        [NoBinding]
        public static async Task WaitForDurableMessage()
        {
            await DurableMessageReceived.WaitAsync().WithTimeout(TimeSpan.FromSeconds(10));
        }


        [NoBinding]
        public static async Task WaitForDynamicMessage()
        {
            await DynamicMessageReceived.WaitAsync().WithTimeout(TimeSpan.FromSeconds(10));
        }


        public ReconnectController(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }


        [DurableQueue("reconnect.durable")]
        public async Task DurableMessage(ReconnectDurableMessage message, CancellationToken cancellationToken)
        {
            testOutputHelper.WriteLine($"- Received message {message.Number} in durable queue");
            DurableMessageReceived.Set();

            if (durableBlock)
                await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        
        [DynamicQueue("reconnect.dynamic")]
        public void NoWaitMessage(ReconnectDynamicMessage message)
        {
            testOutputHelper.WriteLine($"- Received message {message.Number} in dynamic queue");
            DynamicMessageReceived.Set();
        }
    }
}

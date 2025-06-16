using System;
using System.Threading.Tasks;
using Shouldly;
using SimpleInjector;
using Tapeti.Config;
using Tapeti.SimpleInjector;
using Tapeti.Tests.Client.Controller;
using Tapeti.Tests.Helpers;
using Tapeti.Tests.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client
{
    [Collection(RabbitMQCollection.Name)]
    [Trait("Category", "Requires Docker")]
    public class ControllerTests : IAsyncLifetime
    {
        private readonly RabbitMQFixture fixture;
        private readonly ITestOutputHelper testOutputHelper;
        private readonly Container container = new();

        private TapetiConnection? connection;
        private RabbitMQFixture.RabbitMQTestProxy proxy = null!;


        public ControllerTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;
            this.testOutputHelper = testOutputHelper;

            container.RegisterInstance<ILogger>(new MockLogger(testOutputHelper));
            container.RegisterInstance(testOutputHelper);
        }


        public async Task InitializeAsync()
        {
            fixture.LogConnectionInfo(testOutputHelper);

            proxy = await fixture.AcquireProxy();
        }


        public async Task DisposeAsync()
        {
            if (connection != null)
                await connection.DisposeAsync();

            proxy.Dispose();
        }



        [Fact]
        public async Task RequestResponseFilter()
        {
            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .EnableDeclareDurableQueues()
                .RegisterController<RequestResponseFilterController>()
                .Build();

            connection = CreateConnection(config);
            await connection.Subscribe();


            await connection.GetPublisher().PublishRequest<RequestResponseFilterController, FilteredRequestMessage, FilteredResponseMessage>(new FilteredRequestMessage
            {
                ExpectedHandler = 2
            }, c => c.Handler2);


            var handler = await RequestResponseFilterController.ValidResponse.Task.WithTimeout(TimeSpan.FromSeconds(5));
            handler.ShouldBe(2);

            var invalidHandler = await Task.WhenAny(RequestResponseFilterController.InvalidResponse.Task, Task.Delay(1000));
            invalidHandler.ShouldNotBe(RequestResponseFilterController.InvalidResponse.Task);
        }


        [Fact]
        public async Task DedicatedChannel()
        {
            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .EnableDeclareDurableQueues()
                .RegisterController<DedicatedChannelController>()
                .Build();

            connection = CreateConnection(config);
            await connection.Subscribe();


            var publisher = connection.GetPublisher();
            for (var i = 0; i < DedicatedChannelController.WaitMessageCount; i++)
                await publisher.Publish(new DedicatedChannelWaitMessage());

            for (var i = 0; i < DedicatedChannelController.NoWaitMessageCount; i++)
                await publisher.Publish(new DedicatedChannelNoWaitMessage());


            await DedicatedChannelController.WaitForNoWaitMessages();
            await DedicatedChannelController.WaitForWaitMessages();
        }


        [Fact]
        public async Task Reconnect()
        {
            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .EnableDeclareDurableQueues()
                .RegisterController<ReconnectController>()
                .Build();

            connection = CreateConnection(config);

            var disconnectedCompletion = new TaskCompletionSource();
            var reconnectedCompletion = new TaskCompletionSource();

            connection.Disconnected += (_, _) => disconnectedCompletion.TrySetResult();
            connection.Reconnected += (_, _) => reconnectedCompletion.TrySetResult();

            await connection.Subscribe();


            ReconnectController.SetBlockDurableMessage(true);
            await connection.GetPublisher().Publish(new ReconnectDurableMessage { Number = 1 });
            await connection.GetPublisher().Publish(new ReconnectDurableDedicatedMessage { Number = 1 });
            await connection.GetPublisher().Publish(new ReconnectDynamicMessage { Number = 1 });

            // Both messages should arrive. The message for the durable queue will not be acked.
            testOutputHelper.WriteLine("> Waiting for initial messages");
            await Task.WhenAll(ReconnectController.WaitForDurableMessages(), ReconnectController.WaitForDynamicMessage());


            testOutputHelper.WriteLine("> Disabling proxy");
            proxy.RabbitMQProxy.Enabled = false;
            await proxy.RabbitMQProxy.UpdateAsync();

            await disconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));


            testOutputHelper.WriteLine("> Re-enabling proxy");
            ReconnectController.SetBlockDurableMessage(false);


            proxy.RabbitMQProxy.Enabled = true;
            await proxy.RabbitMQProxy.UpdateAsync();

            await reconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));


            // Message in the durable queue should be delivered again
            testOutputHelper.WriteLine("> Waiting for durable message redelivery");
            await ReconnectController.WaitForDurableMessages();


            // Dynamic queue is of course empty but should be recreated
            // Note that the reconnected event fires before the queue is re-declared because that relies on
            // the consume channel to be re-created. So we have to wait for that task to trigger.
            // TODO find a more reliable way to test that the queue has been re-declared - perhaps listen to the logger events?
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            testOutputHelper.WriteLine("> Sending and waiting for dynamic message");
            await connection.GetPublisher().Publish(new ReconnectDynamicMessage { Number = 2 });
            await ReconnectController.WaitForDynamicMessage();
        }


        private TapetiConnection CreateConnection(ITapetiConfig config, ushort prefetchCount = 1, ushort? consumerDispatchConcurrency = null)
        {
            return new TapetiConnection(config)
            {
                Params = new TapetiConnectionParams
                {
                    HostName = "127.0.0.1",
                    Port = proxy.RabbitMQPort,
                    ManagementPort = proxy.RabbitMQManagementPort,
                    Username = RabbitMQFixture.RabbitMQUsername,
                    Password = RabbitMQFixture.RabbitMQPassword,
                    PrefetchCount = prefetchCount,
                    ConsumerDispatchConcurrency = consumerDispatchConcurrency ?? (Environment.ProcessorCount <= ushort.MaxValue ? (ushort)Environment.ProcessorCount : ushort.MaxValue)
                }
            };
        }
    }
}

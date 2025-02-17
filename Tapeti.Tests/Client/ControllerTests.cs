using System;
using System.Threading.Tasks;
using Shouldly;
using SimpleInjector;
using Tapeti.Config;
using Tapeti.SimpleInjector;
using Tapeti.Tests.Client.Controller;
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
        private readonly Container container = new();

        private TapetiConnection? connection;
        private RabbitMQFixture.RabbitMQTestProxy proxy = null!;


        public ControllerTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            container.RegisterInstance<ILogger>(new MockLogger(testOutputHelper));
            container.RegisterInstance(testOutputHelper);
        }


        public async Task InitializeAsync()
        {
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
            await connection!.Subscribe();


            await connection.GetPublisher().PublishRequest<RequestResponseFilterController, FilteredRequestMessage, FilteredResponseMessage>(new FilteredRequestMessage
            {
                ExpectedHandler = 2
            }, c => c.Handler2);


            var handler = await RequestResponseFilterController.ValidResponse.Task;
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

            connection = CreateConnection(config, 50, 2);
            await connection!.Subscribe();


            var publisher = connection.GetPublisher();
            for (var i = 0; i < DedicatedChannelController.WaitMessageCount; i++)
                await publisher.Publish(new DedicatedChannelWaitMessage());

            for (var i = 0; i < DedicatedChannelController.NoWaitMessageCount; i++)
                await publisher.Publish(new DedicatedChannelNoWaitMessage());


            await DedicatedChannelController.WaitForNoWaitMessages();
            await DedicatedChannelController.WaitForWaitMessages();
        }


        private TapetiConnection CreateConnection(ITapetiConfig config, ushort prefetchCount = 1, int? consumerDispatchConcurrency = null)
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
                    ConsumerDispatchConcurrency = consumerDispatchConcurrency ?? Environment.ProcessorCount
                }
            };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Tests.Helpers;
using Tapeti.Tests.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client
{
    [Collection(RabbitMQCollection.Name)]
    [Trait("Category", "Requires Docker")]
    public class TapetiClientTests : IAsyncLifetime
    {
        private readonly RabbitMQFixture fixture;
        private readonly ITestOutputHelper testOutputHelper;
        private readonly MockDependencyResolver dependencyResolver = new();

        private RabbitMQFixture.RabbitMQTestProxy proxy = null!;
        private TapetiClient client = null!;
        private readonly IConnectionEventListener connectionEventListener = Substitute.For<IConnectionEventListener>();


        public TapetiClientTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;
            this.testOutputHelper = testOutputHelper;

            dependencyResolver.Set<ILogger>(new MockLogger(testOutputHelper));
        }


        public async Task InitializeAsync()
        {
            proxy = await fixture.AcquireProxy();
            client = CreateClient();
        }


        public async Task DisposeAsync()
        {
            await client.Close();
            proxy.Dispose();
        }



        [Fact]
        public void Fixture()
        {
            ((int)proxy.RabbitMQPort).ShouldBeGreaterThan(0);
            ((int)proxy.RabbitMQManagementPort).ShouldBeGreaterThan(0);
        }


        [Fact]
        public async Task DynamicQueueDeclareNoPrefix()
        {
            var queueName = await client.DynamicQueueDeclare(null, null, CancellationToken.None);
            queueName.ShouldNotBeNullOrEmpty();
        }


        [Fact]
        public async Task DynamicQueueDeclarePrefix()
        {
            var queueName = await client.DynamicQueueDeclare("dynamicprefix", null, CancellationToken.None);
            queueName.ShouldStartWith("dynamicprefix");
        }


        [Fact]
        public async Task DurableQueueDeclareIncompatibleArguments()
        {
            await using var rabbitmqClient = await CreateRabbitMQClient();
            await using var channel = await rabbitmqClient.CreateChannelAsync();

            var ok = await channel.QueueDeclareAsync("incompatibleargs", true, false, false, new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "d34db33f" }
            });

            await channel.CloseAsync();
            await rabbitmqClient.CloseAsync();


            ok.ShouldNotBeNull();


            await client.DurableQueueDeclare("incompatibleargs", new QueueBinding[]
            {
                new("test", "#")
            }, null, CancellationToken.None);
        }


        [Fact]
        public async Task PublishHandleOverflow()
        {
            var queue1 = await client.DynamicQueueDeclare(null, new RabbitMQArguments
            {
                { "x-max-length", 5 },
                { "x-overflow", "reject-publish" }
            }, CancellationToken.None);

            var queue2 = await client.DynamicQueueDeclare(null, null, CancellationToken.None);

            var body = Encoding.UTF8.GetBytes("Hello world!");
            var properties = new MessageProperties();


            for (var i = 0; i < 5; i++)
                await client.Publish(body, properties, null, queue1, true);


            var publishOverMaxLength = () => client.Publish(body, properties, null, queue1, true);
            await publishOverMaxLength.ShouldThrowAsync<NackException>();

            // The channel should recover and allow further publishing
            await client.Publish(body, properties, null, queue2, true);
        }


        [Fact]
        public async Task Reconnect()
        {
            var disconnectedCompletion = new TaskCompletionSource();
            var reconnectedCompletion = new TaskCompletionSource();

            connectionEventListener
                .When(c => c.Disconnected(Arg.Any<DisconnectedEventArgs>()))
                .Do(_ =>
                {
                    testOutputHelper.WriteLine("Disconnected event triggered");
                    disconnectedCompletion.TrySetResult();
                });

            connectionEventListener
                .When(c => c.Reconnected(Arg.Any<ConnectedEventArgs>()))
                .Do(_ =>
                {
                    testOutputHelper.WriteLine("Reconnected event triggered");
                    reconnectedCompletion.TrySetResult();
                });

            // Trigger the connection to be established
            await client.Publish(Encoding.UTF8.GetBytes("hello, void!"), new MessageProperties(), "nowhere", "nobody", false);


            proxy.RabbitMQProxy.Enabled = false;
            await proxy.RabbitMQProxy.UpdateAsync();


            await disconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));

            proxy.RabbitMQProxy.Enabled = true;
            await proxy.RabbitMQProxy.UpdateAsync();

            await reconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));
        }


        // TODO test the other methods

        private Task<RabbitMQ.Client.IConnection> CreateRabbitMQClient()
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = "127.0.0.1",
                Port = proxy.RabbitMQPort,
                UserName = RabbitMQFixture.RabbitMQUsername,
                Password = RabbitMQFixture.RabbitMQPassword,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false
            };

            return connectionFactory.CreateConnectionAsync();
        }


        private TapetiClient CreateClient()
        {
            return new TapetiClient(
                new TapetiConfig.Config(dependencyResolver),
                new TapetiConnectionParams
                {
                    HostName = "127.0.0.1",
                    Port = proxy.RabbitMQPort,
                    ManagementPort = proxy.RabbitMQManagementPort,
                    Username = RabbitMQFixture.RabbitMQUsername,
                    Password = RabbitMQFixture.RabbitMQPassword,
                    PrefetchCount = 50
                },
                connectionEventListener);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Tests.Mock;
using Tapeti.Transport;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client
{
    [Collection(RabbitMQCollection.Name)]
    [Trait("Category", "Requires Docker")]
    public class TapetiTransportTests : IAsyncLifetime
    {
        private readonly RabbitMQFixture fixture;
        private readonly ITestOutputHelper testOutputHelper;

        private RabbitMQFixture.RabbitMQTestProxy proxy = null!;
        private ITapetiTransport transport = null!;
        private ITapetiTransportChannel channel = null!;
        private readonly ITapetiTransportObserver transportObserver = Substitute.For<ITapetiTransportObserver>();
        private readonly MockLogger logger;


        public TapetiTransportTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;
            this.testOutputHelper = testOutputHelper;

            logger = new MockLogger(testOutputHelper);
        }


        public async Task InitializeAsync()
        {
            fixture.LogConnectionInfo(testOutputHelper);

            proxy = await fixture.AcquireProxy();
            try
            {
                transport = CreateTransport();
                transport.AttachObserver(transportObserver);

                channel = await transport.CreateChannel(new TapetiChannelOptions
                {
                    ChannelType = ChannelType.ConsumeDefault,
                    PublisherConfirmationsEnabled = true
                });
            }
            catch
            {
                // IAsyncLifetime.DisposeAsync will not be called when an exception occurs
                // in InitializeAsync, by design. Ensure the proxy is disposed to prevent deadlocks.
                proxy.Dispose();
                throw;
            }
        }


        public async Task DisposeAsync()
        {
            await transport.Close();
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
            var queueName = await channel.DynamicQueueDeclare(null, null, CancellationToken.None);
            queueName.ShouldNotBeNullOrEmpty();
        }


        [Fact]
        public async Task DynamicQueueDeclarePrefix()
        {
            var queueName = await channel.DynamicQueueDeclare("dynamic_prefix", null, CancellationToken.None);
            queueName.ShouldStartWith("dynamic_prefix");
        }


        [Fact]
        public async Task DurableQueueDeclareIncompatibleArguments()
        {
            await using var rabbitmqClient = await CreateRabbitMQClient();
            await using var incompatibleChannel = await rabbitmqClient.CreateChannelAsync();

            var ok = await incompatibleChannel.QueueDeclareAsync("incompatible_args", true, false, false, new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "d34db33f" }
            });

            await incompatibleChannel.CloseAsync();
            await rabbitmqClient.CloseAsync();


            ok.ShouldNotBeNull();


            await channel.DurableQueueDeclare("incompatible_args", [
                new QueueBinding("test", "#")
            ], null, CancellationToken.None);
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


        private ITapetiTransport CreateTransport()
        {
            var factory = new TapetiTransportFactory(logger);
            return factory.Create(new TapetiConnectionParams
            {
                HostName = "127.0.0.1",
                Port = proxy.RabbitMQPort,
                ManagementPort = proxy.RabbitMQManagementPort,
                Username = RabbitMQFixture.RabbitMQUsername,
                Password = RabbitMQFixture.RabbitMQPassword,
                PrefetchCount = 50
            });
        }
    }
}

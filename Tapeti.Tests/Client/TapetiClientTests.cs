using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Shouldly;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Exceptions;
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
        private readonly MockDependencyResolver dependencyResolver = new();

        private TapetiClient client = null!;


        public TapetiClientTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            dependencyResolver.Set<ILogger>(new MockLogger(testOutputHelper));
        }


        public Task InitializeAsync()
        {
            client = CreateClient();

            return Task.CompletedTask;
        }


        public async Task DisposeAsync()
        {
            await client.Close();
        }



        [Fact]
        public void Fixture()
        {
            ((int)fixture.RabbitMQPort).ShouldBeGreaterThan(0);
            ((int)fixture.RabbitMQManagementPort).ShouldBeGreaterThan(0);
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
            using var rabbitmqClient = CreateRabbitMQClient();
            using var model = rabbitmqClient.CreateModel();

            var ok = model.QueueDeclare("incompatibleargs", true, false, false, new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "d34db33f" }
            });

            model.Close();
            rabbitmqClient.Close();


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


        // TODO test the other methods

        private RabbitMQ.Client.IConnection CreateRabbitMQClient()
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = "127.0.0.1",
                Port = fixture.RabbitMQPort,
                UserName = RabbitMQFixture.RabbitMQUsername,
                Password = RabbitMQFixture.RabbitMQPassword,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false
            };

            return connectionFactory.CreateConnection();
        }


        private TapetiClient CreateClient()
        {
            return new TapetiClient(
                new TapetiConfig.Config(dependencyResolver),
                new TapetiConnectionParams
                {
                    HostName = "127.0.0.1",
                    Port = fixture.RabbitMQPort,
                    ManagementPort = fixture.RabbitMQManagementPort,
                    Username = RabbitMQFixture.RabbitMQUsername,
                    Password = RabbitMQFixture.RabbitMQPassword,
                    PrefetchCount = 50
                },
                null);
        }
    }
}
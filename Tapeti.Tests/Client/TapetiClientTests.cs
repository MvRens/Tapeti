// Do not include in the Release build for AppVeyor due to the Docker requirement
#if DEBUG
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Tests.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client
{
    [Collection(RabbitMQCollection.Name)]
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
            fixture.RabbitMQPort.Should().BeGreaterThan(0);
            fixture.RabbitMQManagementPort.Should().BeGreaterThan(0);
        }


        [Fact]
        public async Task DynamicQueueDeclareNoPrefix()
        {
            var queueName = await client.DynamicQueueDeclare(null, null, CancellationToken.None);
            queueName.Should().NotBeNullOrEmpty();
        }


        [Fact]
        public async Task DynamicQueueDeclarePrefix()
        {
            var queueName = await client.DynamicQueueDeclare("dynamicprefix", null, CancellationToken.None);
            queueName.Should().StartWith("dynamicprefix");
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
            await publishOverMaxLength.Should().ThrowAsync<NackException>();

            // The channel should recover and allow further publishing
            await client.Publish(body, properties, null, queue2, true);
        }


        // TODO test the other methods


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
                });
        }
    }
}
#endif
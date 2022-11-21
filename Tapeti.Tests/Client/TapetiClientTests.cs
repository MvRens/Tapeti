// Do not include in the Release build for AppVeyor due to the Docker requirement
#if DEBUG
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tapeti.Connection;
using Tapeti.Tests.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client
{
    [Collection(RabbitMQCollection.Name)]
    public class TapetiClientTests
    {
        private readonly RabbitMQFixture fixture;
        private readonly MockDependencyResolver dependencyResolver = new();

        public TapetiClientTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            dependencyResolver.Set<ILogger>(new MockLogger(testOutputHelper));
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
            var client = CreateCilent();

            var queueName = await client.DynamicQueueDeclare(null, null, CancellationToken.None);
            queueName.Should().NotBeNullOrEmpty();

            await client.Close();
        }


        [Fact]
        public async Task DynamicQueueDeclarePrefix()
        {
            var client = CreateCilent();

            var queueName = await client.DynamicQueueDeclare("dynamicprefix", null, CancellationToken.None);
            queueName.Should().StartWith("dynamicprefix");

            await client.Close();
        }


        // TODO test the other methods


        private TapetiClient CreateCilent()
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
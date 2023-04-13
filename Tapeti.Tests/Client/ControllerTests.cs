using System.Threading.Tasks;
using FluentAssertions;
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


        public ControllerTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
        {
            this.fixture = fixture;

            container.RegisterInstance<ILogger>(new MockLogger(testOutputHelper));
        }


        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public async Task DisposeAsync()
        {
            if (connection != null)
                await connection.DisposeAsync();
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


            var handler = await RequestResponseFilterController.ValidResponse.Task;
            handler.Should().Be(2);

            var invalidHandler = await Task.WhenAny(RequestResponseFilterController.InvalidResponse.Task, Task.Delay(1000));
            invalidHandler.Should().NotBe(RequestResponseFilterController.InvalidResponse.Task);
        }


        private TapetiConnection CreateConnection(ITapetiConfig config)
        {
            return new TapetiConnection(config)
            {
                Params = new TapetiConnectionParams
                {
                    HostName = "127.0.0.1",
                    Port = fixture.RabbitMQPort,
                    ManagementPort = fixture.RabbitMQManagementPort,
                    Username = RabbitMQFixture.RabbitMQUsername,
                    Password = RabbitMQFixture.RabbitMQPassword,
                    PrefetchCount = 1
                }
            };
        }
    }
}
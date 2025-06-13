using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SimpleInjector;
using Tapeti.Config;
using Tapeti.SimpleInjector;
using Tapeti.Tests.Helpers;
using Tapeti.Tests.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client;

[Collection(RabbitMQCollection.Name)]
[Trait("Category", "Requires Docker")]
public class PublisherTests : IAsyncLifetime
{
    private readonly RabbitMQFixture fixture;
    private readonly ITestOutputHelper testOutputHelper;
    private readonly Container container = new();
    private TapetiConnection? connection;
    private RabbitMQFixture.RabbitMQTestProxy proxy = null!;


    public PublisherTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
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
    public async Task BulkPublish()
    {
        var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
            .Build();

        connection = CreateConnection(config);
        connection.Params = new TapetiConnectionParams
        {
            PublishChannelPoolSize = 16
        };
        await connection.Open();

        var publisher = connection.GetPublisher();


        await Task.WhenAll(
            Enumerable
                .Range(0, 20000)
                .Select(i => Task.Run(() => publisher.Publish(new PublishTestMessage { PublishCount = i })))
            ).WithTimeout(TimeSpan.FromSeconds(30));
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


    [PublicAPI]
    public class PublishTestMessage
    {
        public int PublishCount { get; set; }
    }
}

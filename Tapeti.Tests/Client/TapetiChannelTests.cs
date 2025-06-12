using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Tests.Helpers;
using Tapeti.Tests.Mock;
using Tapeti.Transport;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client;

[Collection(RabbitMQCollection.Name)]
[Trait("Category", "Requires Docker")]
public class TapetiChannelTests : IAsyncLifetime
{
    private readonly RabbitMQFixture fixture;
    private readonly ITestOutputHelper testOutputHelper;

    private RabbitMQFixture.RabbitMQTestProxy proxy = null!;
    private ITapetiTransport transport = null!;
    private TapetiChannel publishChannel = null!;
    private readonly ITapetiTransportObserver transportObserver = Substitute.For<ITapetiTransportObserver>();
    private readonly MockLogger logger;


    public TapetiChannelTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
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

            publishChannel = new TapetiChannel(logger, transport, new TapetiChannelOptions
            {
                ChannelType = ChannelType.PublishDefault,
                PublisherConfirmationsEnabled = true,
                PrefetchCount = 0
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
    public async Task PublishHandleOverflow()
    {
        var body = "Hello world!"u8.ToArray();
        var properties = new MessageProperties();
        string queue1 = null!;
        string queue2 = null!;


        await publishChannel.EnqueueOnce(async channel =>
        {
            queue1 = await channel.DynamicQueueDeclare(null, new RabbitMQArguments
            {
                { "x-max-length", 5 },
                { "x-overflow", "reject-publish" }
            }, CancellationToken.None);

            queue2 = await channel.DynamicQueueDeclare(null, null, CancellationToken.None);

            for (var i = 0; i < 5; i++)
                await channel.Publish(body, properties, null, queue1, true);
        });


        var publishOverMaxLength = () => publishChannel.EnqueueOnce(channel => new ValueTask(channel.Publish(body, properties, null, queue1, true))).AsTask();
        await publishOverMaxLength.ShouldThrowAsync<NackException>();

        // The channel should recover and allow further publishing
        await publishChannel.EnqueueRetry(channel => new ValueTask(channel.Publish(body, properties, null, queue2, true)), CancellationToken.None);
    }


    [Fact]
    public async Task Reconnect()
    {
        var disconnectedCompletion = new TaskCompletionSource();
        var reconnectedCompletion = new TaskCompletionSource();

        transportObserver
            .When(c => c.Disconnected(Arg.Any<DisconnectedEventArgs>()))
            .Do(_ =>
            {
                testOutputHelper.WriteLine("Disconnected event triggered");
                disconnectedCompletion.TrySetResult();
            });

        transportObserver
            .When(c => c.Reconnected(Arg.Any<ConnectedEventArgs>()))
            .Do(_ =>
            {
                testOutputHelper.WriteLine("Reconnected event triggered");
                reconnectedCompletion.TrySetResult();
            });

        await publishChannel.Open();

        proxy.RabbitMQProxy.Enabled = false;
        await proxy.RabbitMQProxy.UpdateAsync();


        await disconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));

        proxy.RabbitMQProxy.Enabled = true;
        await proxy.RabbitMQProxy.UpdateAsync();

        await reconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));
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

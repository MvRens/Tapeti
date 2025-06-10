using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NSubstitute;
using Tapeti.Connection;
using Tapeti.Tests.Helpers;
using Tapeti.Tests.Mock;
using Tapeti.Transport;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Client;

[Collection(RabbitMQCollection.Name)]
[Trait("Category", "Requires Docker")]
public class TapetiSubscriberTests : IAsyncLifetime
{
    private readonly RabbitMQFixture fixture;
    private readonly ITestOutputHelper testOutputHelper;

    private RabbitMQFixture.RabbitMQTestProxy proxy = null!;
    private ITapetiTransport transport = null!;
    private TapetiChannel publishChannel = null!;
    private TapetiChannel consumeChannel = null!;
    private readonly ITapetiTransportObserver transportObserver = Substitute.For<ITapetiTransportObserver>();
    private readonly MockLogger logger;


    public TapetiSubscriberTests(RabbitMQFixture fixture, ITestOutputHelper testOutputHelper)
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

            consumeChannel = new TapetiChannel(logger, transport, new TapetiChannelOptions
            {
                ChannelType = ChannelType.ConsumeDefault,
                PublisherConfirmationsEnabled = false,
                PrefetchCount = 50
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


    #pragma warning disable xUnit1004
    //[Fact(Skip = "Delivery Timeout in RabbitMQ must be at least one minute. Enable this test when refactoring connection logic.")]
    #pragma warning restore xUnit1004
    [Fact]
    public async Task ChannelTimeout()
    {
        var channelShutdown = new TaskCompletionSource();
        var channelRecreated = new TaskCompletionSource();


        await consumeChannel.EnqueueOnce(async channel =>
        {
            await channel.DurableQueueDeclare("channel.timeout.test", [], new RabbitMQArguments
            {
                { "x-consumer-timeout", 60000 }
            }, CancellationToken.None);
        });

        var dependencyResolver = new MockDependencyResolver();
        dependencyResolver.Set<ILogger>(logger);
        dependencyResolver.Set(new TestController(testOutputHelper));

        var config = new TapetiConfig(dependencyResolver)
            .RegisterController<TestController>()
            .Build();

        var subscriber = new TapetiSubscriber(_ => consumeChannel, config);
        await subscriber.ApplyBindings();
        await subscriber.Resume();


        var channelObserver = Substitute.For<ITapetiChannelObserver>();

        #pragma warning disable CA2012
        channelObserver.OnShutdown(Arg.Any<ChannelShutdownEventArgs>())
            .Returns(_ =>
            {
                channelShutdown.TrySetResult();
                return ValueTask.CompletedTask;
            });

        channelObserver.OnRecreated(Arg.Any<ITapetiTransportChannel>())
            .Returns(_ =>
            {
                channelRecreated.TrySetResult();
                return ValueTask.CompletedTask;
            });
        #pragma warning restore CA2012

        consumeChannel.AttachObserver(channelObserver);



        var publisher = new TapetiPublisher(() => publishChannel, config);
        await publisher.PublishDirect(new TestMessage(), "channel.timeout.test", null, true);


        await channelShutdown.Task.WithTimeout(TimeSpan.FromMinutes(3), "Channel shutdown");
        await channelRecreated.Task.WithTimeout(TimeSpan.FromMinutes(2), "Channel re-create");

        await TestController.Redelivered.Task.WithTimeout(TimeSpan.FromSeconds(10), "Message redelivery");
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



    [PublicAPI]
    public class TestMessage
    {
    }


    [PublicAPI]
    [Tapeti.Config.Annotations.DurableQueue("channel.timeout.test")]
    public class TestController
    {
        public static readonly TaskCompletionSource Redelivered = new();

        private readonly ITestOutputHelper testOutputHelper;
        private bool isRedelivery;


        public TestController(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }


        public async Task TestControllerMethod(TestMessage message, CancellationToken cancellationToken)
        {
            if (isRedelivery)
            {
                testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Received message again!");
                Redelivered.SetResult();
            }
            else
            {
                testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Received message, holding until cancelled...");

                isRedelivery = true;
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }
}

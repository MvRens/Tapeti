using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using Tapeti.Connection;
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
                PublisherConfirmationsEnabled = true
            });

            consumeChannel = new TapetiChannel(logger, transport, new TapetiChannelOptions
            {
                ChannelType = ChannelType.ConsumeDefault,
                PublisherConfirmationsEnabled = false
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
        var cancellationTokenSource = new CancellationTokenSource();

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


        var publisher = new TapetiPublisher(() => publishChannel, config);
        await publisher.PublishDirect(new TestMessage(), "channel.timeout.test", null, true);


        var hadConsumer = false;
        var consumerLost = false;
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(3);
        var reconnected = false;

        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(RabbitMQFixture.RabbitMQUsername, RabbitMQFixture.RabbitMQPassword)
        };

        var managementClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        managementClient.DefaultRequestHeaders.Add("Connection", "close");

        var requestUri = new Uri($"http://127.0.0.1:{proxy.RabbitMQManagementPort}/api/queues/%2F/channel.timeout.test");

        while (DateTime.UtcNow < end)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await managementClient.SendAsync(request, CancellationToken.None);
            var responseContent = await response.Content.ReadAsStringAsync(CancellationToken.None);
            var responseData = JsonConvert.DeserializeObject<RabbitMQQueueDetails>(responseContent);

            responseData.ShouldNotBeNull();

            testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Consumers: {responseData.Consumers}");

            if (responseData.Consumers > 0)
            {
                if (consumerLost)
                {
                    testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Consumer reconnected, test successful");
                    reconnected = true;
                    break;
                }

                hadConsumer = true;
            }
            else if (hadConsumer)
            {
                testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Consumer lost, waiting for reconnect");
                consumerLost = true;
                hadConsumer = false;
            }
        }

        if (!reconnected)
            testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Timeout, cancelling test...");

        await cancellationTokenSource.CancelAsync();

        reconnected.ShouldBeTrue();
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



    public class TestMessage
    {
    }

    [PublicAPI]
    [Tapeti.Config.Annotations.DurableQueue("channel.timeout.test")]
    public class TestController
    {
        private readonly ITestOutputHelper testOutputHelper;


        public TestController(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }


        public async Task TestControllerMethod(TestMessage message, CancellationToken cancellationToken)
        {
            testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Received message, holding until cancelled...");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }


    #pragma warning disable CA1822
    #pragma warning restore CA1822


    #pragma warning disable CS0649
    private class RabbitMQQueueDetails
    {
        [JsonProperty("consumers")]
        public int Consumers;
    }
    #pragma warning restore CS0649
}

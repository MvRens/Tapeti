using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Tests.Helpers;
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
        private readonly MockDependencyResolver dependencyResolver = new();

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
            dependencyResolver.Set<ILogger>(logger);
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


        // TODO move to TapetiChannel test, the transport does not recover itself
        [Fact]
        public async Task PublishHandleOverflow()
        {
            var queue1 = await channel.DynamicQueueDeclare(null, new RabbitMQArguments
            {
                { "x-max-length", 5 },
                { "x-overflow", "reject-publish" }
            }, CancellationToken.None);

            var queue2 = await channel.DynamicQueueDeclare(null, null, CancellationToken.None);

            var body = "Hello world!"u8.ToArray();
            var properties = new MessageProperties();


            for (var i = 0; i < 5; i++)
                await channel.Publish(body, properties, null, queue1, true);


            var publishOverMaxLength = () => channel.Publish(body, properties, null, queue1, true);
            await publishOverMaxLength.ShouldThrowAsync<NackException>();

            // The channel should recover and allow further publishing
            await channel.Publish(body, properties, null, queue2, true);
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

            await transport.Open();

            proxy.RabbitMQProxy.Enabled = false;
            await proxy.RabbitMQProxy.UpdateAsync();


            await disconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));

            proxy.RabbitMQProxy.Enabled = true;
            await proxy.RabbitMQProxy.UpdateAsync();

            await reconnectedCompletion.Task.WithTimeout(TimeSpan.FromSeconds(60));
        }



        // TODO move to TapetiChannel test, the transport does not recover itself
        #pragma warning disable xUnit1004
        //[Fact(Skip = "Delivery Timeout in RabbitMQ must be at least one minute. Enable this test when refactoring connection logic.")]
        #pragma warning restore xUnit1004
        [Fact]
        public async Task ChannelTimeout()
        {
            var queueName = await channel.DynamicQueueDeclare(null, new RabbitMQArguments
            {
                { "x-consumer-timeout", 60000 }
            }, CancellationToken.None);

            var cancellationTokenSource = new CancellationTokenSource();

            await channel.Publish("{}"u8.ToArray(), new MessageProperties(), null, queueName, true);
            await channel.Consume(queueName, new TimeoutConsumer(testOutputHelper, cancellationTokenSource.Token), CancellationToken.None);



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

            var requestUri = new Uri($"http://127.0.0.1:{proxy.RabbitMQManagementPort}/api/queues/%2F/{Uri.EscapeDataString(queueName)}");

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


        #pragma warning disable CS0649
        private class RabbitMQQueueDetails
        {
            [JsonProperty("consumers")]
            public int Consumers;
        }
        #pragma warning restore CS0649



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


    public class TimeoutConsumer : IConsumer
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly CancellationToken cancellationToken;

        public TimeoutConsumer(ITestOutputHelper testOutputHelper, CancellationToken cancellationToken)
        {
            this.testOutputHelper = testOutputHelper;
            this.cancellationToken = cancellationToken;
        }

        public async Task<ConsumeResult> Consume(string exchange, string routingKey, IMessageProperties properties, byte[] body)
        {
            testOutputHelper.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Received message, holding until cancelled...");

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ConsumeResult.Success;
        }
    }
}

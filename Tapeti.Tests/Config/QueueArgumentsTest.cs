using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Tapeti.Config.Annotations;
using Tapeti.Config;
using Tapeti.Connection;
using Tapeti.Tests.Mock;
using Tapeti.Transport;
using Xunit;
using Xunit.Abstractions;

namespace Tapeti.Tests.Config
{
    internal static class UTF8StringExtensions
    {
        public static string AsUTF8String(this object value)
        {
            value.ShouldBeOfType<byte[]>();
            return Encoding.UTF8.GetString((byte[])value);
        }
    }


    public class QueueArgumentsTest : BaseControllerTest
    {
        private readonly ITapetiChannel channel;
        private readonly Dictionary<string, IRabbitMQArguments> declaredQueues = new();


        public QueueArgumentsTest(ITestOutputHelper testOutputHelper)
        {
            var transport = Substitute.For<ITapetiTransport>();
            var transportChannel1 = Substitute.For<ITapetiTransportChannel>();
            var logger = new MockLogger(testOutputHelper);

            transport.CreateChannel(Arg.Any<TapetiChannelOptions>())
                .Returns(Task.FromResult(transportChannel1));

            channel = new TapetiChannel(logger, transport, new TapetiChannelOptions
            {
                ChannelType = ChannelType.ConsumeDefault,
                PublisherConfirmationsEnabled = false,
                PrefetchCount = 50
            });


            var routingKeyStrategy = Substitute.For<IRoutingKeyStrategy>();
            var exchangeStrategy = Substitute.For<IExchangeStrategy>();

            DependencyResolver.Set(routingKeyStrategy);
            DependencyResolver.Set(exchangeStrategy);


            routingKeyStrategy
                .GetRoutingKey(typeof(TestMessage1))
                .Returns("testmessage1");

            routingKeyStrategy
                .GetRoutingKey(typeof(TestMessage2))
                .Returns("testmessage2");

            exchangeStrategy
                .GetExchange(Arg.Any<Type>())
                .Returns("exchange");

            var queue = 0;
            transportChannel1
                .DynamicQueueDeclare(null, Arg.Any<IRabbitMQArguments>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    queue++;
                    declaredQueues.Add($"queue-{queue}", callInfo.Arg<IRabbitMQArguments>());

                    return Task.FromResult($"queue-{queue}");
                });

            transportChannel1
                .DurableQueueDeclare(Arg.Any<string>(), Arg.Any<IEnumerable<QueueBinding>>(), Arg.Any<IRabbitMQArguments>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    declaredQueues.Add(callInfo.Arg<string>(), callInfo.Arg<IRabbitMQArguments>());
                    return Task.CompletedTask;
                });


            transportChannel1
                .DynamicQueueBind(Arg.Any<string>(), Arg.Any<QueueBinding>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        }


        [Fact]
        public async Task SingleQueueArguments()
        {
            var config = GetControllerConfig<TestController>();

            var binding1 = config.Bindings.Single(b => b is IControllerMethodBinding { Method.Name: "HandleMessage1" });
            binding1.ShouldNotBeNull();

            var binding2 = config.Bindings.Single(b => b is IControllerMethodBinding { Method.Name: "HandleMessage2" });
            binding2.ShouldNotBeNull();



            var subscriber = new TapetiSubscriber(_ => channel, config);
            await subscriber.ApplyBindings();


            declaredQueues.Count.ShouldBe(1);
            var arguments = declaredQueues["queue-1"];

            arguments["x-custom"].AsUTF8String().ShouldBe("custom value");
            arguments["x-another"].ShouldBe(true);
            arguments["x-max-length"].ShouldBe(100);
            arguments["x-max-length-bytes"].ShouldBe(100000);
            arguments["x-message-ttl"].ShouldBe(4269);
            arguments["x-overflow"].AsUTF8String().ShouldBe("reject-publish");
        }


        [Fact]
        public async Task ConflictingDynamicQueueArguments()
        {
            var config = GetControllerConfig<ConflictingArgumentsTestController>();

            var subscriber = new TapetiSubscriber(_ => channel, config);
            await subscriber.ApplyBindings();

            declaredQueues.Count.ShouldBe(2);

            var arguments1 = declaredQueues["queue-1"];
            arguments1["x-max-length"].ShouldBe(100);

            var arguments2 = declaredQueues["queue-2"];
            arguments2["x-max-length-bytes"].ShouldBe(100000);
        }


        [Fact]
        public async Task ConflictingDurableQueueArguments()
        {
            var config = GetControllerConfig<ConflictingArgumentsDurableQueueTestController>();

            var testApplyBindings = async () =>
            {
                var subscriber = new TapetiSubscriber(_ => channel, config);
                await subscriber.ApplyBindings();
            };

            await testApplyBindings.ShouldThrowAsync<TopologyConfigurationException>();
            declaredQueues.Count.ShouldBe(0);
        }


        // ReSharper disable all
        #pragma warning disable

        private class TestMessage1
        {
        }


        private class TestMessage2
        {
        }


        [DynamicQueue]
        [QueueArguments("x-custom", "custom value", "x-another", true, MaxLength = 100, MaxLengthBytes = 100000, MessageTTL = 4269, Overflow = RabbitMQOverflow.RejectPublish)]
        private class TestController
        {
            public void HandleMessage1(TestMessage1 message)
            {
            }


            public void HandleMessage2(TestMessage2 message)
            {
            }
        }


        [DynamicQueue]
        [QueueArguments(MaxLength = 100)]
        private class ConflictingArgumentsTestController
        {
            public void HandleMessage1(TestMessage1 message)
            {
            }


            [QueueArguments(MaxLengthBytes = 100000)]
            public void HandleMessage2(TestMessage1 message)
            {
            }
        }


        [DurableQueue("durable")]
        [QueueArguments(MaxLength = 100)]
        private class ConflictingArgumentsDurableQueueTestController
        {
            public void HandleMessage1(TestMessage1 message)
            {
            }


            [QueueArguments(MaxLengthBytes = 100000)]
            public void HandleMessage2(TestMessage1 message)
            {
            }
        }

        #pragma warning restore
        // ReSharper restore all
    }
}

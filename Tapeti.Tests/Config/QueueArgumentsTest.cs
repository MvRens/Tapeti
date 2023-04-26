using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Tapeti.Config.Annotations;
using Tapeti.Config;
using Tapeti.Connection;
using Xunit;

namespace Tapeti.Tests.Config
{
    internal static class UTF8StringExtensions
    {
        public static string AsUTF8String(this object value)
        {
            value.Should().BeOfType<byte[]>();
            return Encoding.UTF8.GetString((byte[])value);
        }
    }


    public class QueueArgumentsTest : BaseControllerTest
    {
        private static readonly MockRepository MoqRepository = new(MockBehavior.Strict);

        private readonly Mock<ITapetiClient> client;
        private readonly Dictionary<string, IRabbitMQArguments> declaredQueues = new();


        public QueueArgumentsTest()
        {
            client = MoqRepository.Create<ITapetiClient>();
            var routingKeyStrategy = MoqRepository.Create<IRoutingKeyStrategy>();
            var exchangeStrategy = MoqRepository.Create<IExchangeStrategy>();

            DependencyResolver.Set(routingKeyStrategy.Object);
            DependencyResolver.Set(exchangeStrategy.Object);


            routingKeyStrategy
                .Setup(s => s.GetRoutingKey(typeof(TestMessage1)))
                .Returns("testmessage1");

            routingKeyStrategy
                .Setup(s => s.GetRoutingKey(typeof(TestMessage2)))
                .Returns("testmessage2");

            exchangeStrategy
                .Setup(s => s.GetExchange(It.IsAny<Type>()))
                .Returns("exchange");

            var queue = 0;
            client
                .Setup(c => c.DynamicQueueDeclare(null, It.IsAny<IRabbitMQArguments>(), It.IsAny<CancellationToken>()))
                .Callback((string _, IRabbitMQArguments arguments, CancellationToken _) =>
                {
                    queue++;
                    declaredQueues.Add($"queue-{queue}", arguments);
                })
                .ReturnsAsync(() => $"queue-{queue}");

            client
                .Setup(c => c.DurableQueueDeclare(It.IsAny<string>(), It.IsAny<IEnumerable<QueueBinding>>(), It.IsAny<IRabbitMQArguments>(), It.IsAny<CancellationToken>()))
                .Callback((string queueName, IEnumerable<QueueBinding> _, IRabbitMQArguments arguments, CancellationToken _) =>
                {
                    declaredQueues.Add(queueName, arguments);
                })
                .Returns(Task.CompletedTask);


            client
                .Setup(c => c.DynamicQueueBind(It.IsAny<string>(), It.IsAny<QueueBinding>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }


        [Fact]
        public async Task SingleQueueArguments()
        {
            var config = GetControllerConfig<TestController>();

            var binding1 = config.Bindings.Single(b => b is IControllerMethodBinding { Method.Name: "HandleMessage1" });
            binding1.Should().NotBeNull();

            var binding2 = config.Bindings.Single(b => b is IControllerMethodBinding { Method.Name: "HandleMessage2" });
            binding2.Should().NotBeNull();



            var subscriber = new TapetiSubscriber(() => client.Object, config);
            await subscriber.ApplyBindings();


            declaredQueues.Should().HaveCount(1);
            var arguments = declaredQueues["queue-1"];

            arguments.Should().ContainKey("x-custom").WhoseValue.AsUTF8String().Should().Be("custom value");
            arguments.Should().ContainKey("x-another").WhoseValue.Should().Be(true);
            arguments.Should().ContainKey("x-max-length").WhoseValue.Should().Be(100);
            arguments.Should().ContainKey("x-max-length-bytes").WhoseValue.Should().Be(100000);
            arguments.Should().ContainKey("x-message-ttl").WhoseValue.Should().Be(4269);
            arguments.Should().ContainKey("x-overflow").WhoseValue.AsUTF8String().Should().Be("reject-publish");
        }


        [Fact]
        public async Task ConflictingDynamicQueueArguments()
        {
            var config = GetControllerConfig<ConflictingArgumentsTestController>();

            var subscriber = new TapetiSubscriber(() => client.Object, config);
            await subscriber.ApplyBindings();

            declaredQueues.Should().HaveCount(2);

            var arguments1 = declaredQueues["queue-1"];
            arguments1.Should().ContainKey("x-max-length").WhoseValue.Should().Be(100);

            var arguments2 = declaredQueues["queue-2"];
            arguments2.Should().ContainKey("x-max-length-bytes").WhoseValue.Should().Be(100000);
        }


        [Fact]
        public async Task ConflictingDurableQueueArguments()
        {
            var config = GetControllerConfig<ConflictingArgumentsDurableQueueTestController>();

            var testApplyBindings = () =>
            {
                var subscriber = new TapetiSubscriber(() => client.Object, config);
                return subscriber.ApplyBindings();
            };

            using (new AssertionScope())
            {
                await testApplyBindings.Should().ThrowAsync<TopologyConfigurationException>();
                declaredQueues.Should().HaveCount(0);
            }
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
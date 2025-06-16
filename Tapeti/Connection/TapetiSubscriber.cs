using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Helpers;
using Tapeti.Transport;

namespace Tapeti.Connection
{
    internal enum TapetiSubscriberChannelType
    {
        Default,
        Dedicated
    }


    internal delegate ITapetiChannel ChannelFactory(TapetiSubscriberChannelType channelType);


    /// <inheritdoc />
    internal class TapetiSubscriber : ISubscriber
    {
        private readonly ChannelFactory channelFactory;
        private readonly ITapetiConfig config;
        private bool consuming;
        private readonly List<ITapetiConsumerControl> consumers = [];

        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly IExchangeStrategy exchangeStrategy;
        private readonly ILogger logger;


        public TapetiSubscriber(ChannelFactory channelFactory, ITapetiConfig config)
        {
            this.channelFactory = channelFactory;
            this.config = config;

            routingKeyStrategy = config.DependencyResolver.Resolve<IRoutingKeyStrategy>();
            exchangeStrategy = config.DependencyResolver.Resolve<IExchangeStrategy>();
            logger = config.DependencyResolver.Resolve<ILogger>();
        }


        public async ValueTask DisposeAsync()
        {
            if (consuming)
                await Stop().ConfigureAwait(false);
        }


        public void Dispose()
        {
            if (consuming)
                Stop().GetAwaiter().GetResult();
        }


        /// <inheritdoc />
        public async Task Resume()
        {
            if (consuming)
                return;

            consuming = true;
            await ConsumeQueues().ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task Stop()
        {
            if (!consuming)
                return;

            await Task.WhenAll(consumers.Select(async consumerControl => await consumerControl.Cancel())).ConfigureAwait(false);

            consumers.Clear();
            consuming = false;
        }


        public async ValueTask ApplyBindings()
        {
            CustomBindingTarget bindingTarget;

            // Declaring queues and bindings is always performed on the default channel, the channel
            // used for consuming the queue is determined later on.
            var channel = channelFactory(TapetiSubscriberChannelType.Default);


            await channel.EnqueueOnce(async transportChannel =>
            {
                if (config.GetFeatures().DeclareDurableQueues)
                    bindingTarget = new DeclareDurableQueuesBindingTarget(transportChannel, routingKeyStrategy, exchangeStrategy, transportChannel.ChannelClosed);
                else if (config.GetFeatures().VerifyDurableQueues)
                    bindingTarget = new PassiveDurableQueuesBindingTarget(transportChannel, routingKeyStrategy, exchangeStrategy, transportChannel.ChannelClosed);
                else
                    bindingTarget = new NoVerifyBindingTarget(transportChannel, routingKeyStrategy, exchangeStrategy, transportChannel.ChannelClosed);

                foreach (var binding in config.Bindings)
                    await binding.Apply(bindingTarget).ConfigureAwait(false);

                await bindingTarget.Apply().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private async ValueTask ReapplyDynamicBindings(ITapetiTransportChannel transportChannel, IEnumerable<IBinding> bindings, CancellationToken cancellationToken)
        {
            var bindingTarget = new NoVerifyBindingTarget(transportChannel, routingKeyStrategy, exchangeStrategy, cancellationToken);

            foreach (var binding in bindings)
                await binding.Apply(bindingTarget).ConfigureAwait(false);

            await bindingTarget.Apply().ConfigureAwait(false);
        }


        private async Task ConsumeQueues()
        {
            var queues = config.Bindings.GroupBy(binding =>
            {
                if (string.IsNullOrEmpty(binding.QueueName))
                    throw new InvalidOperationException("QueueName must not be empty");

                return binding.QueueName;
            });

            consumers.AddRange(
                await Task.WhenAll(queues.Select(async group =>
                {
                    var queueName = group.Key;

                    // All should in fact be of the same type. It seems this is not enforced in the code at the moment,
                    // but dynamic queues contain a random generated portion and overlap should be impossible.
                    var isDynamicQueue = group.Any(b => b.QueueType == QueueType.Dynamic);
                    var dynamicQueueBindings = isDynamicQueue ? group.ToArray() : [];

                    var channelType = group.Any(b => b.DedicatedChannel)
                        ? ChannelType.ConsumeDedicated
                        : ChannelType.ConsumeDefault;

                    var channel = channelType == ChannelType.ConsumeDedicated
                        ? channelFactory(TapetiSubscriberChannelType.Dedicated)
                        : channelFactory(TapetiSubscriberChannelType.Default);


                    // ReSharper disable once MoveLocalFunctionAfterJumpStatement
                    async ValueTask<ITapetiTransportConsumer?> Consume(ITapetiTransportChannel transportChannel, bool isRestart)
                    {
                        var consumer = new TapetiConsumer(config, queueName, group, transportChannel.ChannelClosed);
                        var transportConsumer = await transportChannel.Consume(queueName, consumer, channel.MessageHandlerTracker);

                        logger.ConsumeStarted(new ConsumeStartedContext
                        {
                            QueueName = queueName,
                            IsDynamicQueue = isDynamicQueue,
                            IsRestart = isRestart,
                            ChannelType = channelType,
                            ConnectionReference = transportChannel.ConnectionReference,
                            ChannelNumber = transportChannel.ChannelNumber
                        });

                        return transportConsumer;
                    }


                    var transportConsumer = await channel.EnqueueOnce(transportChannel => Consume(transportChannel, false));
                    var control = new TapetiConsumerControl(transportConsumer);

                    channel.AttachObserver(new ChannelRecreatedObserver(newTransportChannel =>
                    {
                        control.SetTransportConsumer(null);

                        // Do not halt the observer while setting up the new bindings
                        _ = Task.Run(async () =>
                        {
                            // Dynamic queues are lost and need to be recreated
                            if (isDynamicQueue)
                            {
                                await ReapplyDynamicBindings(newTransportChannel, dynamicQueueBindings, newTransportChannel.ChannelClosed);

                                Debug.Assert(dynamicQueueBindings[0].QueueName is not null);
                                queueName = dynamicQueueBindings[0].QueueName!;
                            }

                            control.SetTransportConsumer(await Consume(newTransportChannel, true));
                        });

                        return default;
                    }));

                    return (ITapetiConsumerControl)control;
                })).ConfigureAwait(false));
        }


        private class TapetiConsumerControl : ITapetiConsumerControl
        {
            private ITapetiTransportConsumer? transportConsumer;


            public TapetiConsumerControl(ITapetiTransportConsumer? transportConsumer)
            {
                this.transportConsumer = transportConsumer;
            }

            public async Task Cancel()
            {
                if (transportConsumer is null)
                    return;

                // TODO should this through the channel's task queue?
                await transportConsumer.Cancel();
            }


            public void SetTransportConsumer(ITapetiTransportConsumer? newTransportConsumer)
            {
                transportConsumer = newTransportConsumer;
            }
        }


        private abstract class CustomBindingTarget : IBindingTarget
        {
            protected readonly ITapetiTransportChannel TransportChannel;
            protected readonly IRoutingKeyStrategy RoutingKeyStrategy;
            protected readonly IExchangeStrategy ExchangeStrategy;
            protected readonly CancellationToken CancellationToken;

            private struct DynamicQueueInfo
            {
                public string QueueName;
                public List<Type> MessageClasses;
                public IRabbitMQArguments? Arguments;
            }

            private readonly Dictionary<string, List<DynamicQueueInfo>> dynamicQueues = new();


            protected CustomBindingTarget(ITapetiTransportChannel transportChannel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken)
            {
                TransportChannel = transportChannel;
                RoutingKeyStrategy = routingKeyStrategy;
                ExchangeStrategy = exchangeStrategy;
                CancellationToken = cancellationToken;
            }


            public virtual Task Apply()
            {
                return Task.CompletedTask;
            }


            public abstract ValueTask BindDurable(Type messageClass, string queueName, IRabbitMQArguments? arguments);
            public abstract ValueTask BindDurableDirect(string queueName, IRabbitMQArguments? arguments);
            public abstract ValueTask BindDurableObsolete(string queueName);


            public async ValueTask<string> BindDynamic(Type messageClass, string? queuePrefix, IRabbitMQArguments? arguments)
            {
                var result = await DeclareDynamicQueue(messageClass, queuePrefix, arguments).ConfigureAwait(false);
                if (!result.IsNewMessageClass)
                    return result.QueueName;

                var routingKey = RoutingKeyStrategy.GetRoutingKey(messageClass);
                var exchange = ExchangeStrategy.GetExchange(messageClass);

                await TransportChannel.DynamicQueueBind(result.QueueName, new QueueBinding(exchange, routingKey), CancellationToken).ConfigureAwait(false);

                return result.QueueName;
            }


            public async ValueTask<string> BindDynamicDirect(Type messageClass, string? queuePrefix, IRabbitMQArguments? arguments)
            {
                var result = await DeclareDynamicQueue(messageClass, queuePrefix, arguments).ConfigureAwait(false);
                return result.QueueName;
            }


            public async ValueTask<string> BindDynamicDirect(string? queuePrefix, IRabbitMQArguments? arguments)
            {
                // If we don't know the routing key, always create a new queue to ensure there is no overlap.
                // Keep it out of the dynamicQueues dictionary, so it can't be re-used later on either.
                return await TransportChannel.DynamicQueueDeclare(queuePrefix, arguments, CancellationToken).ConfigureAwait(false);
            }


            private struct DeclareDynamicQueueResult
            {
                public string QueueName;
                public bool IsNewMessageClass;
            }

            private async Task<DeclareDynamicQueueResult> DeclareDynamicQueue(Type messageClass, string? queuePrefix, IRabbitMQArguments? arguments)
            {
                // Group by prefix
                var key = queuePrefix ?? "";
                if (!dynamicQueues.TryGetValue(key, out var prefixQueues))
                {
                    prefixQueues = new List<DynamicQueueInfo>();
                    dynamicQueues.Add(key, prefixQueues);
                }

                // Ensure routing keys are unique per dynamic queue, so that a requeue
                // will not cause the side effect of calling another handler again as well.
                foreach (var existingQueueInfo in prefixQueues)
                {
                    // ReSharper disable once InvertIf
                    if (!existingQueueInfo.MessageClasses.Contains(messageClass) && existingQueueInfo.Arguments.NullSafeSameValues(arguments))
                    {
                        // Allow this routing key in the existing dynamic queue
                        var result = new DeclareDynamicQueueResult
                        {
                            QueueName = existingQueueInfo.QueueName,
                            IsNewMessageClass = !existingQueueInfo.MessageClasses.Contains(messageClass)
                        };

                        if (result.IsNewMessageClass)
                            existingQueueInfo.MessageClasses.Add(messageClass);

                        return result;
                    }
                }

                // Declare a new queue
                var queueName = await TransportChannel.DynamicQueueDeclare(queuePrefix, arguments, CancellationToken).ConfigureAwait(false);
                var queueInfo = new DynamicQueueInfo
                {
                    QueueName = queueName,
                    MessageClasses = [messageClass],
                    Arguments = arguments
                };

                prefixQueues.Add(queueInfo);

                return new DeclareDynamicQueueResult
                {
                    QueueName = queueName,
                    IsNewMessageClass = true
                };
            }
        }


        private class DeclareDurableQueuesBindingTarget : CustomBindingTarget
        {
            private struct DurableQueueInfo
            {
                public List<Type> MessageClasses;
                public IRabbitMQArguments? Arguments;
            }


            private readonly Dictionary<string, DurableQueueInfo> durableQueues = new();
            private readonly HashSet<string> obsoleteDurableQueues = [];


            public DeclareDurableQueuesBindingTarget(ITapetiTransportChannel transportChannel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken) : base(transportChannel, routingKeyStrategy, exchangeStrategy, cancellationToken)
            {
            }


            public override ValueTask BindDurable(Type messageClass, string queueName, IRabbitMQArguments? arguments)
            {
                // Collect the message classes per queue so we can determine afterward
                // if any of the bindings currently set on the durable queue are no
                // longer valid and should be removed.
                if (!durableQueues.TryGetValue(queueName, out var durableQueueInfo))
                {
                    durableQueues.Add(queueName, new DurableQueueInfo
                    {
                        MessageClasses = [messageClass],
                        Arguments = arguments
                    });
                }
                else
                {
                    if (!durableQueueInfo.Arguments.NullSafeSameValues(arguments))
                        throw new TopologyConfigurationException($"Multiple conflicting QueueArguments attributes specified for queue {queueName}");

                    if (!durableQueueInfo.MessageClasses.Contains(messageClass))
                        durableQueueInfo.MessageClasses.Add(messageClass);
                }

                return default;
        }


            public override ValueTask BindDurableDirect(string queueName, IRabbitMQArguments? arguments)
            {
                if (!durableQueues.TryGetValue(queueName, out var durableQueueInfo))
                {
                    durableQueues.Add(queueName, new DurableQueueInfo
                    {
                        MessageClasses = new List<Type>(),
                        Arguments = arguments
                    });
                }
                else
                {
                    if (!durableQueueInfo.Arguments.NullSafeSameValues(arguments))
                        throw new TopologyConfigurationException($"Multiple conflicting QueueArguments attributes specified for queue {queueName}");
                }

                return default;
            }


            public override ValueTask BindDurableObsolete(string queueName)
            {
                obsoleteDurableQueues.Add(queueName);
                return default;
            }


            public override async Task Apply()
            {
                await DeclareQueues().ConfigureAwait(false);
                await DeleteObsoleteQueues().ConfigureAwait(false);
            }


            private async Task DeclareQueues()
            {
                foreach (var queue in durableQueues)
                {
                    var bindings = queue.Value.MessageClasses.Select(messageClass =>
                    {
                        var exchange = ExchangeStrategy.GetExchange(messageClass);
                        var routingKey = RoutingKeyStrategy.GetRoutingKey(messageClass);

                        return new QueueBinding(exchange, routingKey);
                    });

                    await TransportChannel.DurableQueueDeclare(queue.Key, bindings, queue.Value.Arguments, CancellationToken).ConfigureAwait(false);
                }
            }


            private async Task DeleteObsoleteQueues()
            {
                foreach (var queue in obsoleteDurableQueues.Except(durableQueues.Keys))
                {
                    await TransportChannel.DurableQueueDelete(queue, true, CancellationToken).ConfigureAwait(false);
                }
            }
        }


        private class PassiveDurableQueuesBindingTarget : CustomBindingTarget
        {
            private readonly HashSet<string> durableQueues = [];


            public PassiveDurableQueuesBindingTarget(ITapetiTransportChannel transportChannel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken) : base(transportChannel, routingKeyStrategy, exchangeStrategy, cancellationToken)
            {
            }


            public override async ValueTask BindDurable(Type messageClass, string queueName, IRabbitMQArguments? arguments)
            {
                await VerifyDurableQueue(queueName, arguments).ConfigureAwait(false);
            }

            public override async ValueTask BindDurableDirect(string queueName, IRabbitMQArguments? arguments)
            {
                await VerifyDurableQueue(queueName, arguments).ConfigureAwait(false);
            }

            public override ValueTask BindDurableObsolete(string queueName)
            {
                return default;
            }


            private async Task VerifyDurableQueue(string queueName, IRabbitMQArguments? arguments)
            {
                if (!durableQueues.Add(queueName))
                    return;

                await TransportChannel.DurableQueueVerify(queueName, arguments, CancellationToken).ConfigureAwait(false);
            }
        }


        private class NoVerifyBindingTarget : CustomBindingTarget
        {
            public NoVerifyBindingTarget(ITapetiTransportChannel transportChannel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken) : base(transportChannel, routingKeyStrategy, exchangeStrategy, cancellationToken)
            {
            }


            public override ValueTask BindDurable(Type messageClass, string queueName, IRabbitMQArguments? arguments)
            {
                return default;
            }

            public override ValueTask BindDurableDirect(string queueName, IRabbitMQArguments? arguments)
            {
                return default;
            }

            public override ValueTask BindDurableObsolete(string queueName)
            {
                return default;
            }
        }
    }

    internal class ChannelRecreatedObserver : ITapetiChannelObserver
    {
        private readonly Func<ITapetiTransportChannel, ValueTask> onRecreated;


        public ChannelRecreatedObserver(Func<ITapetiTransportChannel, ValueTask> onRecreated)
        {
            this.onRecreated = onRecreated;
        }


        public ValueTask OnShutdown(ChannelShutdownEventArgs e)
        {
            return default;
        }

        public ValueTask OnRecreated(ITapetiTransportChannel newChannel)
        {
            return onRecreated(newChannel);
        }
    }
}

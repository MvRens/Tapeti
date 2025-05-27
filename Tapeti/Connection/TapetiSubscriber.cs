using System;
using System.Collections.Generic;
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

        private CancellationTokenSource? initializeCancellationTokenSource;


        public TapetiSubscriber(ChannelFactory channelFactory, ITapetiConfig config)
        {
            this.channelFactory = channelFactory;
            this.config = config;
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



        // TODO change to per-channel
        /// <summary>
        /// Applies the configured bindings and declares the queues in RabbitMQ. For internal use only.
        /// </summary>
        /// <returns></returns>
        public async Task ApplyBindings()
        {
            initializeCancellationTokenSource = new CancellationTokenSource();
            await ApplyBindings(initializeCancellationTokenSource.Token).ConfigureAwait(false);
        }


        // TODO is this still relevant?
        /// <summary>
        /// Called after the connection is lost. For internal use only.
        /// Guaranteed to be called from within the taskQueue thread.
        /// </summary>
        public void Disconnect()
        {
            initializeCancellationTokenSource?.Cancel();
            initializeCancellationTokenSource = null;

            consumers.Clear();
        }


        // TODO is this still relevant?
        /// <summary>
        /// Called after the connection is lost and regained. Reapplies the bindings and if Resume
        /// has already been called, restarts the consumers. For internal use only.
        /// Guaranteed to be called from within the taskQueue thread.
        /// </summary>
        public void Reconnect()
        {
            initializeCancellationTokenSource?.Cancel();
            initializeCancellationTokenSource = new CancellationTokenSource();

            consumers.Clear();

            var cancellationToken = initializeCancellationTokenSource.Token;

            Task.Run(async () =>
            {
                await ApplyBindings(cancellationToken).ConfigureAwait(false);

                if (consuming && !cancellationToken.IsCancellationRequested)
                    await ConsumeQueues(cancellationToken).ConfigureAwait(false);
            }, CancellationToken.None);
        }


        /// <inheritdoc />
        public async Task Resume()
        {
            if (consuming)
                return;

            consuming = true;
            initializeCancellationTokenSource = new CancellationTokenSource();

            await ConsumeQueues(initializeCancellationTokenSource.Token).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task Stop()
        {
            if (!consuming)
                return;

            if (initializeCancellationTokenSource is not null)
            {
                await initializeCancellationTokenSource.CancelAsync();
                initializeCancellationTokenSource = null;
            }

            await Task.WhenAll(consumers.Select(async tag => await tag.Cancel())).ConfigureAwait(false);

            consumers.Clear();
            consuming = false;
        }


        private async ValueTask ApplyBindings(CancellationToken cancellationToken)
        {
            var routingKeyStrategy = config.DependencyResolver.Resolve<IRoutingKeyStrategy>();
            var exchangeStrategy = config.DependencyResolver.Resolve<IExchangeStrategy>();

            CustomBindingTarget bindingTarget;

            // Declaring queues and bindings is always performed on the default channel, the channel
            // used for consuming the queue is determined later on.
            var channel = channelFactory(TapetiSubscriberChannelType.Default);


            // TODO keep track of declared queues and exchanges
            // this used to be handled by the TapetiClient, but it seems wrong to make the transport keep track of this

            if (config.GetFeatures().DeclareDurableQueues)
                bindingTarget = new DeclareDurableQueuesBindingTarget(channel, routingKeyStrategy, exchangeStrategy, cancellationToken);
            else if (config.GetFeatures().VerifyDurableQueues)
                bindingTarget = new PassiveDurableQueuesBindingTarget(channel, routingKeyStrategy, exchangeStrategy, cancellationToken);
            else
                bindingTarget = new NoVerifyBindingTarget(channel, routingKeyStrategy, exchangeStrategy, cancellationToken);

            foreach (var binding in config.Bindings)
                await binding.Apply(bindingTarget).ConfigureAwait(false);

            await bindingTarget.Apply().ConfigureAwait(false);
        }


        private async Task ConsumeQueues(CancellationToken cancellationToken)
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
                    var consumer = new TapetiConsumer(config, queueName, group, cancellationToken);

                    var channel = group.Any(b => b.DedicatedChannel)
                        ? channelFactory(TapetiSubscriberChannelType.Dedicated)
                        : channelFactory(TapetiSubscriberChannelType.Default);


                    // ReSharper disable once MoveLocalFunctionAfterJumpStatement
                    Task<ITapetiTransportConsumer?> Consume(ITapetiTransportChannel transportChannel)
                    {
                        return transportChannel.Consume(queueName, consumer, cancellationToken);
                    }


                    var transportConsumer = await channel.Enqueue(Consume);
                    var control = new TapetiConsumerControl(transportConsumer);

                    channel.AttachObserver(new ChannelRecreatedObserver(async newTransportChannel =>
                    {
                        control.SetTransportConsumer(await Consume(newTransportChannel));
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

                await transportConsumer.Cancel();
            }


            public void SetTransportConsumer(ITapetiTransportConsumer? newTransportConsumer)
            {
                transportConsumer = newTransportConsumer;
            }
        }


        private abstract class CustomBindingTarget : IBindingTarget
        {
            protected readonly ITapetiChannel Channel;
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


            protected CustomBindingTarget(ITapetiChannel channel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken)
            {
                Channel = channel;
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

                await Channel.Enqueue(transportChannel => transportChannel.DynamicQueueBind(result.QueueName, new QueueBinding(exchange, routingKey), CancellationToken)).ConfigureAwait(false);

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
                return await Channel.Enqueue(transportChannel => transportChannel.DynamicQueueDeclare(queuePrefix, arguments, CancellationToken)).ConfigureAwait(false);
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
                // will not cause the side-effect of calling another handler again as well.
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
                var queueName = await Channel.Enqueue(transportChannel => transportChannel.DynamicQueueDeclare(queuePrefix, arguments, CancellationToken)).ConfigureAwait(false);
                var queueInfo = new DynamicQueueInfo
                {
                    QueueName = queueName,
                    MessageClasses = new List<Type> { messageClass },
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
            private readonly HashSet<string> obsoleteDurableQueues = new();


            public DeclareDurableQueuesBindingTarget(ITapetiChannel channel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken) : base(channel, routingKeyStrategy, exchangeStrategy, cancellationToken)
            {
            }


            public override ValueTask BindDurable(Type messageClass, string queueName, IRabbitMQArguments? arguments)
            {
                // Collect the message classes per queue so we can determine afterwards
                // if any of the bindings currently set on the durable queue are no
                // longer valid and should be removed.
                if (!durableQueues.TryGetValue(queueName, out var durableQueueInfo))
                {
                    durableQueues.Add(queueName, new DurableQueueInfo
                    {
                        MessageClasses = new List<Type>
                        {
                            messageClass
                        },
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
                await Task.WhenAll(durableQueues.Select(async queue =>
                {
                    var bindings = queue.Value.MessageClasses.Select(messageClass =>
                    {
                        var exchange = ExchangeStrategy.GetExchange(messageClass);
                        var routingKey = RoutingKeyStrategy.GetRoutingKey(messageClass);

                        return new QueueBinding(exchange, routingKey);
                    });

                    await Channel.Enqueue(transportChannel => transportChannel.DurableQueueDeclare(queue.Key, bindings, queue.Value.Arguments, CancellationToken)).ConfigureAwait(false);
                })).ConfigureAwait(false);
            }


            private async Task DeleteObsoleteQueues()
            {
                await Task.WhenAll(obsoleteDurableQueues.Except(durableQueues.Keys).Select(async queue =>
                {
                    await Channel.Enqueue(transportChannel => transportChannel.DurableQueueDelete(queue, true, CancellationToken)).ConfigureAwait(false);
                })).ConfigureAwait(false);
            }
        }


        private class PassiveDurableQueuesBindingTarget : CustomBindingTarget
        {
            private readonly HashSet<string> durableQueues = new();


            public PassiveDurableQueuesBindingTarget(ITapetiChannel channel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken) : base(channel, routingKeyStrategy, exchangeStrategy, cancellationToken)
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

                await Channel.Enqueue(transportChannel => transportChannel.DurableQueueVerify(queueName, arguments, CancellationToken)).ConfigureAwait(false);
            }
        }


        private class NoVerifyBindingTarget : CustomBindingTarget
        {
            public NoVerifyBindingTarget(ITapetiChannel channel, IRoutingKeyStrategy routingKeyStrategy, IExchangeStrategy exchangeStrategy, CancellationToken cancellationToken) : base(channel, routingKeyStrategy, exchangeStrategy, cancellationToken)
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
            throw new NotImplementedException();
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

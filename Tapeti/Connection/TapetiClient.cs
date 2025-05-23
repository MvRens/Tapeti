using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Exceptions;
using Tapeti.Helpers;
using Tapeti.Transport;

namespace Tapeti.Connection
{
    /// <summary>
    /// Implementation of ITapetiClient for the RabbitMQ Client library
    /// </summary>
    /*
    internal class TapetiClient : ITapetiClient
    {
        private const int MandatoryReturnTimeout = 300000;
        private const int CloseMessageHandlersTimeout = 30000;

        private readonly TapetiConnectionParams connectionParams;

        private readonly ITapetiConfig config;
        private readonly ILogger logger;


        private readonly TapetiClientConnection connection;
        private readonly TapetiChannel defaultConsumeChannel;
        private readonly TapetiChannel defaultPublishChannel;
        private readonly List<TapetiChannel> dedicatedChannels = new();

        private readonly MessageHandlerTracker messageHandlerTracker = new();

        // These fields are for use in a single TapetiChannel's queue only!
        private ulong lastDeliveryTag;
        private readonly HashSet<string> deletedQueues = new();

        // These fields must be locked using confirmLock, since the callbacks for BasicAck/BasicReturn can run in a different thread
        private readonly object confirmLock = new();
        private readonly Dictionary<ulong, ConfirmMessageInfo> confirmMessages = new();
        private readonly Dictionary<string, ReturnInfo> returnRoutingKeys = new();


        private class ConfirmMessageInfo
        {
            public string ReturnKey { get; }
            public TaskCompletionSource<int> CompletionSource { get; }


            public ConfirmMessageInfo(string returnKey, TaskCompletionSource<int> completionSource)
            {
                ReturnKey = returnKey;
                CompletionSource = completionSource;
            }
        }


        private class ReturnInfo
        {
            public uint RefCount;
            public int FirstReplyCode;
        }


        public TapetiClient(ITapetiConfig config, TapetiConnectionParams connectionParams, IConnectionEventListener? connectionEventListener)
        {
            this.config = config;
            this.connectionParams = connectionParams;

            logger = config.DependencyResolver.Resolve<ILogger>();

            connection = new TapetiClientConnection(logger, connectionParams)
            {
                ConnectionEventListener = connectionEventListener
            };

            defaultConsumeChannel = connection.CreateChannel(null, InitConsumeModel);
            defaultPublishChannel = connection.CreateChannel(() => config.GetFeatures().PublisherConfirms, InitPublishModel);

            //TODO connection.OnQueueReconnect += () => defaultConsumeChannel.QueueRetryable(_ => Task.CompletedTask);

        }


        private async ValueTask InitConsumeModel(IChannel channel)
        {
            if (connectionParams.PrefetchCount > 0)
                await channel.BasicQosAsync(0, connectionParams.PrefetchCount, false);
        }


        private ValueTask InitPublishModel(IChannel channel)
        {
            if (config.GetFeatures().PublisherConfirms)
            {
                lastDeliveryTag = 0;

                Monitor.Enter(confirmLock);
                try
                {
                    foreach (var pair in confirmMessages)
                        pair.Value.CompletionSource.SetCanceled();

                    confirmMessages.Clear();
                }
                finally
                {
                    Monitor.Exit(confirmLock);
                }
            }

            channel.BasicReturnAsync += HandleBasicReturn;
            channel.BasicAcksAsync += HandleBasicAck;
            channel.BasicNacksAsync += HandleBasicNack;

            return default;
        }


        /// <inheritdoc />
        public async Task Publish(byte[] body, IMessageProperties properties, string? exchange, string routingKey, bool mandatory)
        {
            if (string.IsNullOrEmpty(routingKey))
                throw new ArgumentNullException(nameof(routingKey));


            await defaultPublishChannel.Enqueue(async transportChannel =>
            {
                // TODO rely on client's built-in publisher confirms?
                Task<int>? publishResultTask = null;
                var messageInfo = new ConfirmMessageInfo(GetReturnKey(exchange ?? string.Empty, routingKey), new TaskCompletionSource<int>());

                // TODO ported from WithRetryableChannel, should be improved
                while (true)
                {
                    try
                    {
                        if (exchange != null)
                            await DeclareExchange(transportChannel, exchange);

                        // The delivery tag is lost after a reconnect, register under the new tag
                        if (config.GetFeatures().PublisherConfirms)
                        {
                            lastDeliveryTag++;

                            Monitor.Enter(confirmLock);
                            try
                            {
                                confirmMessages.Add(lastDeliveryTag, messageInfo);
                            }
                            finally
                            {
                                Monitor.Exit(confirmLock);
                            }

                            publishResultTask = messageInfo.CompletionSource.Task;
                        }
                        else
                            mandatory = false;

                        try
                        {
                            await transportChannel.BasicPublishAsync(exchange ?? string.Empty, routingKey, mandatory, properties.ToBasicProperties(), body);
                        }
                        catch
                        {
                            messageInfo.CompletionSource.SetCanceled();
                            publishResultTask = null;

                            throw;
                        }

                        break;
                    }
                    catch (AlreadyClosedException)
                    {
                    }
                }


                if (publishResultTask == null)
                    return;

                var delayCancellationTokenSource = new CancellationTokenSource();
                var signalledTask = await Task.WhenAny(
                    publishResultTask,
                    Task.Delay(MandatoryReturnTimeout, delayCancellationTokenSource.Token)).ConfigureAwait(false);

                if (signalledTask != publishResultTask)
                    throw new TimeoutException(
                        $"Timeout while waiting for basic.return for message with exchange '{exchange}' and routing key '{routingKey}'");

                delayCancellationTokenSource.Cancel();

                if (publishResultTask.IsCanceled)
                    throw new NackException(
                        $"Mandatory message with with exchange '{exchange}' and routing key '{routingKey}' was nacked");

                var replyCode = publishResultTask.Result;

                switch (replyCode)
                {
                    // There is no RabbitMQ.Client.Framing.Constants value for this "No route" reply code
                    // at the time of writing...
                    case 312:
                        throw new NoRouteException(
                            $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' does not have a route");

                    case > 0:
                        throw new NoRouteException(
                            $"Mandatory message with exchange '{exchange}' and routing key '{routingKey}' could not be delivered, reply code: {replyCode}");
                }
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        /*
        public async Task<ITapetiConsumerTag?> Consume(string queueName, IConsumer consumer, TapetiConsumeOptions options, CancellationToken cancellationToken)
        {
            if (deletedQueues.Contains(queueName))
                return null;

            if (string.IsNullOrEmpty(queueName))
                throw new ArgumentNullException(nameof(queueName));


            long capturedConnectionReference = -1;
            string? consumerTag = null;

            var channel = options.DedicatedChannel
                ? CreateDedicatedConsumeChannel()
                : defaultConsumeChannel;

            await channel.QueueRetryable(async innerChannel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                capturedConnectionReference = connection.GetConnectionReference();
                var basicConsumer = new TapetiBasicConsumer(innerChannel, consumer, messageHandlerTracker, capturedConnectionReference,
                    (connectionReference, deliveryTag, result) => Respond(channel, connectionReference, deliveryTag, result));

                consumerTag = await innerChannel.BasicConsumeAsync(queueName, false, basicConsumer, cancellationToken: CancellationToken.None);
            }).ConfigureAwait(false);

            return consumerTag == null
                ? null
                : new TapetiConsumerTag(this, channel, consumerTag, capturedConnectionReference);
        }
        *


        private async Task Cancel(TapetiChannel channel, string consumerTag, long connectionReference)
        {
            if (connection.IsClosing || string.IsNullOrEmpty(consumerTag))
                return;

            var capturedConnectionReference = connection.GetConnectionReference();

            // If the connection was re-established in the meantime, don't respond with an
            // invalid deliveryTag. The message will be requeued.
            if (capturedConnectionReference != connectionReference)
                return;

            // No need for a retryable channel here, if the connection is lost
            // so is the consumer.
            await channel.Queue(async innerChannel =>
            {
                // Check again as a reconnect may have occured in the meantime
                var currentConnectionReference = connection.GetConnectionReference();
                if (currentConnectionReference != connectionReference)
                    return;

                await innerChannel.BasicCancelAsync(consumerTag);
            }).ConfigureAwait(false);
        }


        private async Task Respond(TapetiChannel channel, long expectedConnectionReference, ulong deliveryTag, ConsumeResult result)
        {
            await channel.Queue(async innerChannel =>
            {
                // If the connection was re-established in the meantime, don't respond with an
                // invalid deliveryTag. The message will be requeued.
                var currentConnectionReference = connection.GetConnectionReference();
                if (currentConnectionReference != expectedConnectionReference)
                    return;

                // No need for a retryable channel here, if the connection is lost we can't
                // use the deliveryTag anymore.
                switch (result)
                {
                    case ConsumeResult.Success:
                    case ConsumeResult.ExternalRequeue:
                        await innerChannel.BasicAckAsync(deliveryTag, false);
                        break;

                    case ConsumeResult.Error:
                        await innerChannel.BasicNackAsync(deliveryTag, false, false);
                        break;

                    case ConsumeResult.Requeue:
                        await innerChannel.BasicNackAsync(deliveryTag, false, true);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, null);
                }
            }).ConfigureAwait(false);
        }








        /// <inheritdoc />
        public async Task DurableQueueVerify(string queueName, IRabbitMQArguments? arguments, CancellationToken cancellationToken)
        {
            if (!await GetDurableQueueDeclareRequired(queueName, arguments).ConfigureAwait(false))
                return;

            await defaultConsumeChannel.Queue(async innerChannel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                (logger as IBindingLogger)?.QueueDeclare(queueName, true, true);
                await innerChannel.QueueDeclarePassiveAsync(queueName, cancellationToken);
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task DurableQueueDelete(string queueName, bool onlyIfEmpty, CancellationToken cancellationToken)
        {
            if (!onlyIfEmpty)
            {
                uint deletedMessages = 0;

                await defaultConsumeChannel.Queue(async innerChannel =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    deletedMessages = await innerChannel.QueueDeleteAsync(queueName, cancellationToken: cancellationToken);
                }).ConfigureAwait(false);

                deletedQueues.Add(queueName);
                (logger as IBindingLogger)?.QueueObsolete(queueName, true, deletedMessages);
                return;
            }


            await defaultConsumeChannel.QueueWithProvider(async channelProvider =>
            {
                bool retry;
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    retry = false;

                    // Get queue information from the Management API, since the AMQP operations will
                    // throw an error if the queue does not exist or still contains messages and resets
                    // the connection. The resulting reconnect will cause subscribers to reset.
                    var queueInfo = await GetQueueInfo(queueName).ConfigureAwait(false);
                    if (queueInfo == null)
                    {
                        deletedQueues.Add(queueName);
                        return;
                    }

                    if (queueInfo.Messages == 0)
                    {
                        // Still pass onlyIfEmpty to prevent concurrency issues if a message arrived between
                        // the call to the Management API and deleting the queue. Because the QueueWithRetryableChannel
                        // includes the GetQueueInfo, the next time around it should have Messages > 0
                        try
                        {
                            await channelProvider.WithChannel(async innerChannel =>
                            {
                                await innerChannel.QueueDeleteAsync(queueName, false, true, cancellationToken: cancellationToken);
                            });

                            deletedQueues.Add(queueName);
                            (logger as IBindingLogger)?.QueueObsolete(queueName, true, 0);
                        }
                        catch (OperationInterruptedException e)
                        {
                            if (e.ShutdownReason?.ReplyCode == Constants.PreconditionFailed)
                                retry = true;
                            else
                                throw;
                        }
                    }
                    else
                    {
                        // Remove all bindings instead
                        var existingBindings = (await GetQueueBindings(queueName).ConfigureAwait(false)).ToList();

                        if (existingBindings.Count > 0)
                        {
                            await channelProvider.WithChannel(async innerChannel =>
                            {
                                foreach (var binding in existingBindings)
                                    await innerChannel.QueueUnbindAsync(queueName, binding.Exchange, binding.RoutingKey, cancellationToken: cancellationToken);
                            });
                        }

                        (logger as IBindingLogger)?.QueueObsolete(queueName, false, queueInfo.Messages);
                    }
                } while (retry);
            }).ConfigureAwait(false);
        }



        /// <inheritdoc />
        public async Task DynamicQueueBind(string queueName, QueueBinding binding, CancellationToken cancellationToken)
        {
            await defaultConsumeChannel.Queue(async innerChannel =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await DeclareExchange(innerChannel, binding.Exchange);
                (logger as IBindingLogger)?.QueueBind(queueName, false, binding.Exchange, binding.RoutingKey);
                await innerChannel.QueueBindAsync(queueName, binding.Exchange, binding.RoutingKey, cancellationToken: cancellationToken);
            }).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task Close()
        {
            // Empty the queue
            await defaultConsumeChannel.Close().ConfigureAwait(false);
            await defaultPublishChannel.Close().ConfigureAwait(false);

            foreach (var channel in dedicatedChannels)
                await channel.Close().ConfigureAwait(false);

            dedicatedChannels.Clear();
            await connection.Close();


            // Wait for message handlers to finish
            await messageHandlerTracker.WaitForIdle(CloseMessageHandlersTimeout);
        }

















        private TapetiChannel CreateDedicatedConsumeChannel()
        {
            var channel = connection.CreateChannel(null, InitConsumeModel);
            dedicatedChannels.Add(channel);

            return channel;
        }


        private Task HandleBasicReturn(object? sender, BasicReturnEventArgs e)
        {
            /*
             * "If the message is also published as mandatory, the basic.return is sent to the client before basic.ack."
             * - https://www.rabbitmq.com/confirms.html
             *
             * Because there is no delivery tag included in the basic.return message. This solution is modeled after
             * user OhJeez' answer on StackOverflow:
             *
             * "Since all messages with the same routing key are routed the same way. I assumed that once I get a
             *  basic.return about a specific routing key, all messages with this routing key can be considered undelivered"
             * https://stackoverflow.com/questions/21336659/how-to-tell-which-amqp-message-was-not-routed-from-basic-return-response
             *
            var key = GetReturnKey(e.Exchange, e.RoutingKey);

            if (!returnRoutingKeys.TryGetValue(key, out var returnInfo))
            {
                returnInfo = new ReturnInfo
                {
                    RefCount = 0,
                    FirstReplyCode = e.ReplyCode
                };

                returnRoutingKeys.Add(key, returnInfo);
            }

            returnInfo.RefCount++;
            return Task.CompletedTask;
        }


        private Task HandleBasicAck(object? sender, BasicAckEventArgs e)
        {
            Monitor.Enter(confirmLock);
            try
            {
                foreach (var deliveryTag in GetDeliveryTags(e))
                {
                    if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo))
                        continue;

                    if (returnRoutingKeys.TryGetValue(messageInfo.ReturnKey, out var returnInfo))
                    {
                        messageInfo.CompletionSource.SetResult(returnInfo.FirstReplyCode);

                        returnInfo.RefCount--;
                        if (returnInfo.RefCount == 0)
                            returnRoutingKeys.Remove(messageInfo.ReturnKey);
                    }
                    else
                        messageInfo.CompletionSource.SetResult(0);

                    confirmMessages.Remove(deliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }

            return Task.CompletedTask;
        }


        private Task HandleBasicNack(object? sender, BasicNackEventArgs e)
        {
            Monitor.Enter(confirmLock);
            try
            {
                foreach (var deliveryTag in GetDeliveryTags(e))
                {
                    if (!confirmMessages.TryGetValue(deliveryTag, out var messageInfo))
                        continue;

                    messageInfo.CompletionSource.SetCanceled();
                    confirmMessages.Remove(e.DeliveryTag);
                }
            }
            finally
            {
                Monitor.Exit(confirmLock);
            }

            return Task.CompletedTask;
        }


        private IEnumerable<ulong> GetDeliveryTags(BasicAckEventArgs e)
        {
            return e.Multiple
                ? confirmMessages.Keys.Where(tag => tag <= e.DeliveryTag).ToArray()
                : new[] { e.DeliveryTag };
        }


        private IEnumerable<ulong> GetDeliveryTags(BasicNackEventArgs e)
        {
            return e.Multiple
                ? confirmMessages.Keys.Where(tag => tag <= e.DeliveryTag).ToArray()
                : new[] { e.DeliveryTag };
        }


        private static string GetReturnKey(string exchange, string routingKey)
        {
            return exchange + ':' + routingKey;
        }


        private class TapetiConsumerTag : ITapetiConsumerTag
        {
            private readonly TapetiClient client;
            private readonly TapetiChannel channel;

            public string ConsumerTag { get; }
            public long ConnectionReference { get; }


            public TapetiConsumerTag(TapetiClient client, TapetiChannel channel, string consumerTag, long connectionReference)
            {
                this.client = client;
                this.channel = channel;

                ConnectionReference = connectionReference;
                ConsumerTag = consumerTag;
            }


            public Task Cancel()
            {
                return client.Cancel(channel, ConsumerTag, ConnectionReference);
            }
        }

    }
    */
}

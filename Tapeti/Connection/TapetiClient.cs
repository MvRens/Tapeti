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
        private const int CloseMessageHandlersTimeout = 30000;

        private readonly TapetiConnectionParams connectionParams;

        private readonly ITapetiConfig config;
        private readonly ILogger logger;


        private readonly TapetiClientConnection connection;
        private readonly TapetiChannel defaultConsumeChannel;
        private readonly TapetiChannel defaultPublishChannel;
        private readonly List<TapetiChannel> dedicatedChannels = new();

        private readonly MessageHandlerTracker messageHandlerTracker = new();



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

            return default;
        }



        /// <inheritdoc />
        /*
        public async Task<ITapetiConsumerTag?> Consume(string queueName, IConsumer consumer, TapetiConsumeOptions options, CancellationToken cancellationToken)
        {

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

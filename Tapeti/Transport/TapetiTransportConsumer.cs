using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Default;

namespace Tapeti.Transport
{
    /// <summary>
    /// Implements the bridge between the RabbitMQ Client consumer and a Tapeti Consumer
    /// </summary>
    internal class TapetiTransportConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IConsumer consumer;
        private readonly IMessageHandlerTracker messageHandlerTracker;
        private readonly CancellationToken cancellationToken;


        /// <inheritdoc />
        public TapetiTransportConsumer(IChannel channel, IConsumer consumer,
            IMessageHandlerTracker messageHandlerTracker, CancellationToken cancellationToken) : base(channel)
        {
            this.consumer = consumer;
            this.messageHandlerTracker = messageHandlerTracker;
            this.cancellationToken = cancellationToken;
        }


        /// <inheritdoc />
        public override async Task HandleBasicDeliverAsync(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            // At the time of writing, RabbitMQ.NET's AsyncConsumerDispatcher does not pass any cancellation token
            CancellationToken uselessCancellationToken = default)
        {
            messageHandlerTracker.Enter();
            try
            {
                // RabbitMQ.Client 6+ re-uses the body memory. Unfortunately Newtonsoft.Json does not support deserializing
                // from Span/ReadOnlyMemory yet so we still need to use ToArray and allocate heap memory for it. When support
                // is implemented we need to rethink the way the body is passed around and maybe deserialize it sooner
                // (which changes exception handling, which is now done in TapetiConsumer exclusively).
                //
                // See also: https://github.com/JamesNK/Newtonsoft.Json/issues/1761
                var bodyArray = body.ToArray();

                try
                {

                    // If the message handler hangs and does not respond to the channel's CancellationToken, the
                    // connection will eventually get an 'End of stream' making recovery difficult. Spawning a new
                    // Task will allow us to guard against this scenario.
                    var consumeTask = Task.Run(async () =>
                    {
                        var response = await consumer.Consume(exchange, routingKey, properties.ToMessageProperties(), bodyArray).ConfigureAwait(false);
                        await Respond(deliveryTag, response).ConfigureAwait(false);
                    }, cancellationToken);


                    try
                    {
                        var cancelledTask = new TaskCompletionSource();
                        await using var cancelledTaskRegistration = cancellationToken.Register(() => cancelledTask.TrySetCanceled());

                        var completedTask = Task.WhenAny(consumeTask, cancelledTask.Task);
                        if (completedTask == consumeTask)
                        {
                            if (!consumeTask.IsCompletedSuccessfully)
                                // Await to throw any exceptions
                                await consumeTask.ConfigureAwait(false);
                        }
                        else
                        {
                            // Return from HandleBasicDeliverAsync to prevent the connection from clogging,
                            // track the consumeTask externally.
                            messageHandlerTracker.Detach(consumeTask);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            throw;
                    }
                }
                catch
                {
                    await Respond(deliveryTag, ConsumeResult.Error).ConfigureAwait(false);
                }
            }
            finally
            {
                messageHandlerTracker.Exit();
            }
        }


        private async Task Respond(ulong deliveryTag, ConsumeResult result)
        {
            // If the connection was closed in the meantime, don't respond with an
            // invalid deliveryTag. The message will be re-queued.
            if (Channel.IsClosed)
                return;

            // No need for a retryable channel here, if the connection is lost we can't
            // use the deliveryTag anymore.
            switch (result)
            {
                case ConsumeResult.Success:
                case ConsumeResult.ExternalRequeue:
                    await Channel.BasicAckAsync(deliveryTag, false, CancellationToken.None);
                    break;

                case ConsumeResult.Error:
                    await Channel.BasicNackAsync(deliveryTag, false, false, CancellationToken.None);
                    break;

                case ConsumeResult.Requeue:
                    await Channel.BasicNackAsync(deliveryTag, false, true, CancellationToken.None);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}

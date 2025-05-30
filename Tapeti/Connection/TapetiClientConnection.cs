using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    internal readonly struct TapetiChannelReference
    {
        public IChannel Channel { get; }
        public long ConnectionReference { get; }
        public DateTime CreatedDateTime { get; }


        public TapetiChannelReference(IChannel channel, long connectionReference, DateTime createdDateTime)
        {
            Channel = channel;
            ConnectionReference = connectionReference;
            CreatedDateTime = createdDateTime;
        }
    }


    /// <summary>
    /// Implements a resilient connection to RabbitMQ.
    /// </summary>
    internal class TapetiClientConnection
    {
        /// <summary>
        /// Receives events when the connection state changes.
        /// </summary>
        //public IConnectionEventListener? ConnectionEventListener { get; set; }

        public event Action? OnQueueReconnect;






        private readonly ILogger logger;
        private readonly TapetiConnectionParams connectionParams;

        private readonly ConnectionFactory connectionFactory;




        public TapetiClientConnection(ILogger logger, TapetiConnectionParams connectionParams)
        {
            this.logger = logger;
            this.connectionParams = connectionParams;

        }


        public async Task Close()
        {

        }


        public TapetiChannel CreateChannel(Func<bool>? usePublisherConfirms, Func<IChannel, ValueTask>? onInitChannel)
        {
            var capturedChannel = new WeakReference<TapetiChannel?>(null);
            /* TODO no longer relevant?
            var channel = new TapetiChannel(channelReference => AcquireChannel(channelReference, usePublisherConfirms is not null && usePublisherConfirms(), async innerChannel =>
            {
                innerChannel.ChannelShutdownAsync += (_, _) =>
                {
                    if (capturedChannel.TryGetTarget(out var weakCapturedChannel))
                        weakCapturedChannel.ClearModel();

                    return Task.CompletedTask;
                };

                if (onInitChannel is not null)
                    await onInitChannel.Invoke(innerChannel);
            }));
            */

            //capturedChannel.SetTarget(channel);
            //return channel;
            return null!;
        }
    }
}

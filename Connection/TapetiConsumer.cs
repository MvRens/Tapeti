using System;
using System.Diagnostics.Eventing.Reader;
using RabbitMQ.Client;

namespace Tapeti.Connection
{
    public class TapetiConsumer : DefaultBasicConsumer
    {
        private readonly TapetiWorker worker;
        private readonly IMessageSerializer messageSerializer;
        private readonly IQueueRegistration queueRegistration;


        public TapetiConsumer(TapetiWorker worker, IMessageSerializer messageSerializer, IQueueRegistration queueRegistration)
        {
            this.worker = worker;
            this.messageSerializer = messageSerializer;
            this.queueRegistration = queueRegistration;
        }


        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IBasicProperties properties, byte[] body)
        {
            try
            {
                var message = messageSerializer.Deserialize(body, properties);
                if (message == null)
                    throw new ArgumentException("Empty message");

                if (queueRegistration.Accept(message))
                    queueRegistration.Visit(message);
                else
                    throw new ArgumentException($"Unsupported message type: {message.GetType().FullName}");

                worker.Respond(deliveryTag, ConsumeResponse.Ack);
            }
            catch (Exception)
            {
                //TODO pluggable exception handling
                worker.Respond(deliveryTag, ConsumeResponse.Nack);
            }
        }
    }
}

using RabbitMQ.Client;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd.Commands
{
    public class ExportCommand
    {
        public IMessageSerializer MessageSerializer { get; set; }

        public string QueueName { get; set; }
        public bool RemoveMessages { get; set; }
        public int? MaxCount { get; set; }


        public int Execute(IModel channel)
        {
            var messageCount = 0;

            while (!MaxCount.HasValue || messageCount < MaxCount.Value)
            {
                var result = channel.BasicGet(QueueName, false);
                if (result == null)
                    // No more messages on the queue
                    break;

                messageCount++;

                MessageSerializer.Serialize(new Message
                {
                    DeliveryTag = result.DeliveryTag,
                    Redelivered = result.Redelivered,
                    Exchange = result.Exchange,
                    RoutingKey = result.RoutingKey,
                    Queue = QueueName,
                    Properties = result.BasicProperties,
                    Body = result.Body
                });

                if (RemoveMessages)
                    channel.BasicAck(result.DeliveryTag, false);
            }

            return messageCount;
        }
    }
}

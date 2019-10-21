using RabbitMQ.Client;
 
namespace Tapeti.Cmd.Commands
{
    public class ShovelCommand
    {
        public string QueueName { get; set; }
        public string TargetQueueName { get; set; }
        public bool RemoveMessages { get; set; }
        public int? MaxCount { get; set; }


        public int Execute(IModel sourceChannel, IModel targetChannel)
        {
            var messageCount = 0;

            while (!MaxCount.HasValue || messageCount < MaxCount.Value)
            {
                var result = sourceChannel.BasicGet(QueueName, false);
                if (result == null)
                    // No more messages on the queue
                    break;


                targetChannel.BasicPublish("", TargetQueueName, result.BasicProperties, result.Body);


                messageCount++;

                if (RemoveMessages)
                    sourceChannel.BasicAck(result.DeliveryTag, false);
            }

            return messageCount;
        }
    }
}

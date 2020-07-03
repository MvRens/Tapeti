using RabbitMQ.Client;
using Tapeti.Cmd.RateLimiter;
using Tapeti.Cmd.Serialization;

namespace Tapeti.Cmd.Commands
{
    public class ImportCommand
    {
        public IMessageSerializer MessageSerializer { get; set; }

        public bool DirectToQueue { get; set; }


        public int Execute(IModel channel, IRateLimiter rateLimiter)
        {
            var messageCount = 0;

            foreach (var message in MessageSerializer.Deserialize())
            {
                rateLimiter.Execute(() =>
                {
                    var exchange = DirectToQueue ? "" : message.Exchange;
                    var routingKey = DirectToQueue ? message.Queue : message.RoutingKey;

                    channel.BasicPublish(exchange, routingKey, message.Properties, message.Body);
                    messageCount++;
                });
            }

            return messageCount;
        }
    }
}

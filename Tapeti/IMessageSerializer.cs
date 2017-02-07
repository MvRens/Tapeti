using RabbitMQ.Client;

namespace Tapeti
{
    public interface IMessageSerializer
    {
        byte[] Serialize(object message, IBasicProperties properties);
        object Deserialize(byte[] body, IBasicProperties properties);
    }
}

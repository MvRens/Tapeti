using Tapeti.Config;

namespace Tapeti
{
    /// <summary>
    /// Provides serialization and deserialization for messages.
    /// </summary>
    public interface IMessageSerializer
    {
        /// <summary>
        /// Serialize a message object instance to a byte array.
        /// </summary>
        /// <param name="message">An instance of a message class</param>
        /// <param name="properties">Writable access to the message properties which will be sent along with the message</param>
        /// <returns>The encoded message</returns>
        byte[] Serialize(object message, IMessageProperties properties);

        /// <summary>
        /// Deserializes a previously serialized message.
        /// </summary>
        /// <param name="body">The encoded message</param>
        /// <param name="properties">The properties as sent along with the message</param>
        /// <returns>A decoded instance of the message</returns>
        object? Deserialize(byte[] body, IMessageProperties properties);
    }
}

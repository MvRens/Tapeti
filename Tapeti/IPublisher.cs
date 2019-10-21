using System.Threading.Tasks;
using Tapeti.Config;

// ReSharper disable once UnusedMember.Global

namespace Tapeti
{
    /// <summary>
    /// Allows publishing of messages.
    /// </summary>
    public interface IPublisher
    {
        /// <summary>
        /// Publish the specified message. Transport details are determined by the Tapeti configuration.
        /// </summary>
        /// <param name="message">The message to send</param>
        Task Publish(object message);
    }


    /// <inheritdoc />
    /// <summary>
    /// Low-level publisher for Tapeti internal use.
    /// </summary>
    /// <remarks>
    /// Tapeti assumes every implementation of IPublisher can also be cast to an IInternalPublisher.
    /// The distinction is made on purpose to trigger code-smells in non-Tapeti code when casting.
    /// </remarks>
    public interface IInternalPublisher : IPublisher
    {
        /// <summary>
        /// Publishes a message. The exchange and routing key are determined by the registered strategies.
        /// </summary>
        /// <param name="message">An instance of a message class</param>
        /// <param name="properties">Metadata to include in the message</param>
        /// <param name="mandatory">If true, an exception will be raised if the message can not be delivered to at least one queue</param>
        Task Publish(object message, IMessageProperties properties, bool mandatory);


        /// <summary>
        /// Publishes a message directly to a queue. The exchange and routing key are not used.
        /// </summary>
        /// <param name="message">An instance of a message class</param>
        /// <param name="queueName">The name of the queue to send the message to</param>
        /// <param name="properties">Metadata to include in the message</param>
        /// <param name="mandatory">If true, an exception will be raised if the message can not be delivered to the queue</param>
        /// <returns></returns>
        Task PublishDirect(object message, string queueName, IMessageProperties properties, bool mandatory);
    }
}

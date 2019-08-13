using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Connection
{
    /// <inheritdoc cref="IEquatable{T}" />
    /// <summary>
    /// Defines a queue binding to an exchange using a routing key
    /// </summary>
    public struct QueueBinding : IEquatable<QueueBinding>
    {
        /// <summary></summary>
        public readonly string Exchange;

        /// <summary></summary>
        public readonly string RoutingKey;


        /// <summary>
        /// Initializes a new QueueBinding
        /// </summary>
        /// <param name="exchange"></param>
        /// <param name="routingKey"></param>
        public QueueBinding(string exchange, string routingKey)
        {
            Exchange = exchange;
            RoutingKey = routingKey;
        }


        /// <inheritdoc />
        public bool Equals(QueueBinding other)
        {
            return string.Equals(Exchange, other.Exchange) && string.Equals(RoutingKey, other.RoutingKey);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is QueueBinding other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Exchange != null ? Exchange.GetHashCode() : 0) * 397) ^ (RoutingKey != null ? RoutingKey.GetHashCode() : 0);
            }
        }
    }


    /// <summary>
    /// Provides a bridge between Tapeti and the actual RabbitMQ client
    /// </summary>
    public interface ITapetiClient
    {
        /// <summary>
        /// Publishes a message. The exchange and routing key are determined by the registered strategies.
        /// </summary>
        /// <param name="body">The raw message data to publish</param>
        /// <param name="properties">Metadata to include in the message</param>
        /// <param name="exchange">The exchange to publish the message to, or empty to send it directly to a queue</param>
        /// <param name="routingKey">The routing key for the message, or queue name if exchange is empty</param>
        /// <param name="mandatory">If true, an exception will be raised if the message can not be delivered to at least one queue</param>
        Task Publish(byte[] body, IMessageProperties properties, string exchange, string routingKey, bool mandatory);


        /// <summary>
        /// Starts a consumer for the specified queue, using the provided bindings to handle messages.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="consumer">The consumer implementation which will receive the messages from the queue</param>
        Task Consume(string queueName, IConsumer consumer);


        /// <summary>
        /// Creates a durable queue if it does not already exist, and updates the bindings.
        /// </summary>
        /// <param name="queueName">The name of the queue to create</param>
        /// <param name="bindings">A list of bindings. Any bindings already on the queue which are not in this list will be removed</param>
        Task DurableQueueDeclare(string queueName, IEnumerable<QueueBinding> bindings);

        /// <summary>
        /// Verifies a durable queue exists. Will raise an exception if it does not.
        /// </summary>
        /// <param name="queueName">The name of the queue to verify</param>
        Task DurableQueueVerify(string queueName);

        /// <summary>
        /// Creates a dynamic queue.
        /// </summary>
        /// <param name="queuePrefix">An optional prefix for the dynamic queue's name. If not provided, RabbitMQ's default logic will be used to create an amq.gen queue.</param>
        Task<string> DynamicQueueDeclare(string queuePrefix = null);

        /// <summary>
        /// Add a binding to a dynamic queue.
        /// </summary>
        /// <param name="queueName">The name of the dynamic queue previously created using DynamicQueueDeclare</param>
        /// <param name="binding">The binding to add to the dynamic queue</param>
        Task DynamicQueueBind(string queueName, QueueBinding binding);


        /// <summary>
        /// Closes the connection to RabbitMQ gracefully.
        /// </summary>
        Task Close();
    }
}
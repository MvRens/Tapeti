using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Tapeti.Config.Annotations
{
    /// <summary>
    /// Determines the overflow behaviour of a queue that has reached it's maximum as set by <see cref="QueueArgumentsAttribute.MaxLength"/> or <see cref="QueueArgumentsAttribute.MaxLengthBytes"/>.
    /// </summary>
    [PublicAPI]
    public enum RabbitMQOverflow
    {
        /// <summary>
        /// The argument will not be explicitly specified and use the RabbitMQ default, which is equivalent to <see cref="DropHead"/>.
        /// </summary>
        NotSpecified,

        /// <summary>
        /// Discards or dead-letters the oldest published message. This is the default value.
        /// </summary>
        DropHead,

        /// <summary>
        /// Discards the most recently published messages and nacks the message.
        /// </summary>
        RejectPublish,

        /// <summary>
        /// Dead-letters the most recently published messages and nacks the message.
        /// </summary>
        RejectPublishDeadletter
    }


    /// <summary>
    /// Specifies the optional queue arguments (also known as 'x-arguments') used when declaring
    /// the queue.
    /// </summary>
    /// <remarks>
    /// The QueueArguments attribute can be applied to any controller or method and will affect the queue
    /// that is used in that context. For durable queues, at most one QueueArguments attribute can be specified
    /// per unique queue name.
    /// <br/><br/>
    /// Also note that queue arguments can not be changed after a queue is declared. You should declare a new queue
    /// and make the old one Obsolete to have Tapeti automatically removed it once it is empty. Tapeti will use the
    /// existing queue, but log a warning at startup time.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    [PublicAPI]
    public class QueueArgumentsAttribute : Attribute
    {
        /// <summary>
        /// The maximum number of messages in the queue. Set <see cref="Overflow"/> to determine the overflow behaviour.
        /// </summary>
        /// <remarks>
        /// Corresponds to 'max-length'. See <see href="https://www.rabbitmq.com/maxlength.html"/>
        /// </remarks>
        public int MaxLength { get; set; }

        /// <summary>
        /// The maximum number of bytes in the queue (counting only the message bodies). Set <see cref="Overflow"/> to determine the overflow behaviour.
        /// </summary>
        /// <remarks>
        /// Corresponds to 'x-max-length-bytes'. See <see href="https://www.rabbitmq.com/maxlength.html"/>
        /// </remarks>
        public int MaxLengthBytes { get; set; }


        /// <inheritdoc cref="RabbitMQOverflow"/>
        /// <remarks>
        /// Corresponds to 'x-overflow'. Default is to drop or deadletter the oldest messages in the queue. See <see href="https://www.rabbitmq.com/maxlength.html"/>
        /// </remarks>
        public RabbitMQOverflow Overflow { get; set; } = RabbitMQOverflow.NotSpecified;


        /// <summary>
        /// Specifies the maximum Time-to-Live for messages in the queue, in milliseconds.
        /// </summary>
        /// <remarks>
        /// Corresponds to 'x-message-ttl'. See <see href="https://www.rabbitmq.com/ttl.html" />
        /// </remarks>
        public int MessageTTL { get; set; }


        /// <summary>
        /// Any arguments to add which are not supported by properties of QueueArguments.
        /// </summary>
        public IReadOnlyDictionary<string, object> CustomArguments { get; private set; }


        /// <inheritdoc cref="QueueArgumentsAttribute"/>
        /// <param name="customArguments">Any arguments to add which are not supported by properties of QueueArguments. Must be a multiple of 2, specify each key followed by the value.</param>
        public QueueArgumentsAttribute(params object[] customArguments)
        {
            if (customArguments.Length % 2 != 0)
                throw new ArgumentException("customArguments must be a multiple of 2 to specify each key-value combination", nameof(customArguments));

            var customArgumentsPairs = new Dictionary<string, object>();

            for (var i = 0; i < customArguments.Length; i += 2)
                customArgumentsPairs[(string)customArguments[i]] = customArguments[i + 1];

            CustomArguments = customArgumentsPairs;
        }
    }
}

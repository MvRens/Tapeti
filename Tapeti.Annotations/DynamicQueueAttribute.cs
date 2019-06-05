using System;
using JetBrains.Annotations;

namespace Tapeti.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// Creates a non-durable auto-delete queue to receive messages. Can be used
    /// on an entire MessageController class or on individual methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    [MeansImplicitUse]
    public class DynamicQueueAttribute : Attribute
    {
        /// <summary>
        /// An optional prefix. If specified, Tapeti will compose the queue name using the
        /// prefix and a unique ID. If not specified, an empty queue name will be passed
        /// to RabbitMQ thus letting it create a unique queue name.
        /// </summary>
        public string Prefix { get; set; }


        /// <inheritdoc />
        /// <param name="prefix">An optional prefix. If specified, Tapeti will compose the queue name using the
        /// prefix and a unique ID. If not specified, an empty queue name will be passed
        /// to RabbitMQ thus letting it create a unique queue name.</param>
        public DynamicQueueAttribute(string prefix = null)
        {
            Prefix = prefix;
        }
    }
}

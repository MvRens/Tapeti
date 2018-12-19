using System;

namespace Tapeti.Annotations
{
    /// <summary>
    /// Creates a non-durable auto-delete queue to receive messages. Can be used
    /// on an entire MessageController class or on individual methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DynamicQueueAttribute : Attribute
    {
        public string Prefix { get; set; }


        /// <summary>
        /// If prefix is specified, Tapeti will compose the queue name using the
        /// prefix and a unique ID. If not specified, an empty queue name will be passed
        /// to RabbitMQ thus letting it create a unique queue name.
        /// </summary>
        /// <param name="prefix"></param>
        public DynamicQueueAttribute(string prefix = null)
        {
            Prefix = prefix;
        }
    }
}

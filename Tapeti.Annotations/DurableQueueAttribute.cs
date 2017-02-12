using System;

namespace Tapeti.Annotations
{
    /// <summary>
    /// Binds to an existing durable queue to receive messages. Can be used
    /// on an entire MessageController class or on individual methods.
    /// </summary>
    /// <remarks>
    /// At the moment there is no support for creating a durable queue and managing the
    /// bindings. The author recommends https://git.x2software.net/pub/RabbitMetaQueue
    /// for deploy-time management of durable queues (shameless plug intended).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DurableQueueAttribute : Attribute
    {
        public string Name { get; set; }


        public DurableQueueAttribute(string name)
        {
            Name = name;
        }
    }
}

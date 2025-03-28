using System;

namespace Tapeti.Config.Annotations
{
    /// <summary>
    /// Requests a dedicated RabbitMQ Channel for consuming messages from the queue
    /// the queue.
    /// </summary>
    /// <remarks>
    /// The DedicatedChannel attribute can be applied to any controller or method and will apply to the queue
    /// that is used in that context. It does not need be applied to all message handlers for that queue to have
    /// an effect.
    /// <br/><br/>
    /// The intended use case is for high-traffic message handlers, or message handlers which can block for either
    /// a long time or indefinitely for throttling purposes. These can clog up the channel's workers and impact
    /// other queues.
    /// </remarks>
    public class DedicatedChannelAttribute : Attribute
    {
    }
}

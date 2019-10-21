namespace Tapeti
{
    /// <summary>
    /// Determines how the message has been handled and the response given to the message bus.
    /// </summary>
    public enum ConsumeResult
    {
        /// <summary>
        /// Acknowledge the message and remove it from the queue.
        /// </summary>
        Success,

        /// <summary>
        /// Negatively acknowledge the message and remove it from the queue, send to dead-letter queue if configured on the bus.
        /// </summary>
        Error,

        /// <summary>
        /// Negatively acknowledge the message and put it back in the queue to try again later.
        /// </summary>
        Requeue,

        /// <summary>
        /// The message has been stored for republishing and will be delivered again by some other means.
        /// It will be acknowledged and removed from the queue as if succesful.
        /// </summary>
        /// <remarks>
        /// This option is for compatibility with external scheduler services. The exception strategy must guarantee that the
        /// message will eventually be republished.
        /// </remarks>
        ExternalRequeue
    }
}

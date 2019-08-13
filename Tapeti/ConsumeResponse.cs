namespace Tapeti
{
    /// <summary>
    /// Determines the response sent back after handling a message.
    /// </summary>
    public enum ConsumeResponse
    {
        /// <summary>
        /// Acknowledge the message and remove it from the queue
        /// </summary>
        Ack,

        /// <summary>
        /// Negatively acknowledge the message and remove it from the queue, send to dead-letter queue if configured on the bus
        /// </summary>
        Nack,

        /// <summary>
        /// Negatively acknowledge the message and put it back in the queue to try again later
        /// </summary>
        Requeue
    }
}

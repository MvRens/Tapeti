// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <summary>
    /// Indicates how the message was handled.
    /// </summary>
    public enum MessageAction
    {
        /// <summary>
        /// The message was handled succesfully.
        /// </summary>
        Success,

        /// <summary>
        /// There was an error while processing the message.
        /// </summary>
        Error,
        
        /// <summary>
        /// The message has been stored for republishing and will be delivered again
        /// even if the current messages has been Acked or Nacked.
        /// </summary>
        /// <remarks>
        /// This option is for compatibility with external scheduler services that do not immediately requeue a message.
        /// </remarks>
        ExternalRetry
    }
}

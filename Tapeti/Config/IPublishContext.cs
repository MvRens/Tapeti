// ReSharper disable UnusedMember.Global

namespace Tapeti.Config
{
    /// <summary>
    /// Provides access to information about the message being published.
    /// </summary>
    public interface IPublishContext
    {
        /// <summary>
        /// Provides access to the Tapeti config.
        /// </summary>
        ITapetiConfig Config { get; }

        /// <summary>
        /// The exchange to which the message will be published.
        /// </summary>
        string Exchange { get; set; }

        /// <summary>
        /// The routing key which will be included with the message.
        /// </summary>
        string RoutingKey { get; }

        /// <summary>
        /// The instance of the message class.
        /// </summary>
        object Message { get; }

        /// <summary>
        /// Provides access to the message metadata.
        /// </summary>
        IMessageProperties Properties { get; }
    }
}

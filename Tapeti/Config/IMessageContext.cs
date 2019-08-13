using System;

namespace Tapeti.Config
{
    /// <inheritdoc />
    /// <summary>
    /// Provides information about the message currently being handled.
    /// </summary>
    public interface IMessageContext : IDisposable
    {
        /// <summary>
        /// Provides access to the Tapeti config.
        /// </summary>
        ITapetiConfig Config { get; }

        /// <summary>
        /// Contains the name of the queue the message was consumed from.
        /// </summary>
        string Queue { get; }

        /// <summary>
        /// Contains the exchange to which the message was published.
        /// </summary>
        string Exchange { get; }

        /// <summary>
        /// Contains the routing key as provided when the message was published.
        /// </summary>
        string RoutingKey { get; }

        /// <summary>
        /// Contains the decoded message instance.
        /// </summary>
        object Message { get; }

        /// <summary>
        /// Provides access to the message metadata.
        /// </summary>
        IMessageProperties Properties { get; }

        /// <remarks>
        /// Provides access to the binding which is currently processing the message.
        /// </remarks>
        IBinding Binding { get; }
    }
}

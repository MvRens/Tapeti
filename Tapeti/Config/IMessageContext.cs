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


        /// <summary>
        /// Stores a key-value pair in the context for passing information between the various
        /// middleware stages (mostly for IControllerMiddlewareBase descendants).
        /// </summary>
        /// <param name="key">A unique key. It is recommended to prefix it with the package name which hosts the middleware to prevent conflicts</param>
        /// <param name="value">Will be disposed if the value implements IDisposable</param>
        void Store(string key, object value);

        /// <summary>
        /// Retrieves a previously stored value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>True if the value was found, False otherwise</returns>
        bool Get<T>(string key, out T value) where T : class;
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

// ReSharper disable UnusedMemberInSuper.Global - public API
// ReSharper disable UnusedMember.Global

namespace Tapeti.Config
{
    /// <summary>
    /// Provides information about the message currently being handled.
    /// </summary>
    public interface IMessageContext : IAsyncDisposable
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
        /// Contains the raw body of the message.
        /// </summary>
        byte[]? RawBody { get; }

        /// <summary>
        /// Contains the decoded message instance.
        /// </summary>
        object? Message { get; }

        /// <summary>
        /// Provides access to the message metadata.
        /// </summary>
        IMessageProperties Properties { get; }

        /// <remarks>
        /// Provides access to the binding which is currently processing the message.
        /// </remarks>
        IBinding Binding { get; }

        /// <summary>
        /// Contains a CancellationToken which is cancelled when the connection to the RabbitMQ server is closed.
        /// Note that this token is cancelled regardless of whether the connection will be reestablished, as any
        /// messages still in the queue will be redelivered with a new token.
        /// </summary>
        // TODO change to cancellationtoken per channel
        CancellationToken ConnectionClosed { get; }

        /// <summary>
        /// Stores additional properties in the message context which can be passed between middleware stages.
        /// </summary>
        /// <remarks>
        /// Only one instance of type T is stored, if Enrich was called before for this type an InvalidOperationException will be thrown.
        /// </remarks>
        /// <param name="payload">A class implementing IMessageContextPayload</param>
        void Store<T>(T payload) where T : IMessageContextPayload;

        /// <summary>
        /// Stored a new payload, or updates an existing one.
        /// </summary>
        /// <param name="onAdd">A method returning the new payload to be stored</param>
        /// <param name="onUpdate">A method called when the payload exists</param>
        /// <typeparam name="T">The payload type as passed to Enrich</typeparam>
        void StoreOrUpdate<T>(Func<T> onAdd, Action<T> onUpdate) where T : IMessageContextPayload;

        /// <summary>
        /// Returns the properties as previously stored with Enrich. Throws a KeyNotFoundException
        /// if the payload is not stored in this message context.
        /// </summary>
        /// <typeparam name="T">The payload type as passed to Enrich</typeparam>
        T Get<T>() where T : IMessageContextPayload;


        /// <summary>
        /// Returns true and the payload value if this message context was previously enriched with the payload T.
        /// </summary>
        /// <typeparam name="T">The payload type as passed to Enrich</typeparam>
        bool TryGet<T>([NotNullWhen(true)] out T? payload) where T : IMessageContextPayload;

        /// <summary>
        /// Stores a key-value pair in the context for passing information between the various
        /// middleware stages (mostly for IControllerMiddlewareBase descendants).
        /// </summary>
        /// <param name="key">A unique key. It is recommended to prefix it with the package name which hosts the middleware to prevent conflicts</param>
        /// <param name="value">Will be disposed if the value implements IDisposable or IAsyncDisposable</param>
        [Obsolete("For backwards compatibility only. Use Store<T> payload for typed properties instead")]
        void Store(string key, object value);

        /// <summary>
        /// Retrieves a previously stored value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>True if the value was found, False otherwise</returns>
        [Obsolete("For backwards compatibility only. Use Get<T> payload overload for typed properties instead")]
        bool Get<T>(string key, out T? value) where T : class;
    }


    /// <summary>
    /// Base interface for additional properties added to the message context.
    /// </summary>
    /// <remarks>
    /// Descendants implementing IDisposable or IAsyncDisposable will be disposed along with the message context.
    /// </remarks>
    public interface IMessageContextPayload
    {
    }
}

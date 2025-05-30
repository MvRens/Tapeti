using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace Tapeti
{
    /// <summary>
    /// Contains information about the established connection.
    /// </summary>
    [PublicAPI]
    public class ConnectedEventArgs
    {
        /// <summary>
        /// The connection parameters used to establish the connection.
        /// </summary>
        public required TapetiConnectionParams ConnectionParams { get; init; }

        /// <summary>
        /// The local port for the connection. Useful for identifying the connection in the management interface.
        /// </summary>
        public required int LocalPort { get; init; }
    }


    /// <summary>
    /// Contains information about the reason for a lost connection.
    /// </summary>
    [PublicAPI]
    public class DisconnectedEventArgs
    {
        /// <summary>
        /// The ReplyCode as indicated by the client library
        /// </summary>
        public required ushort ReplyCode { get; init; }

        /// <summary>
        /// The ReplyText as indicated by the client library
        /// </summary>
        public required string ReplyText { get; init; }
    }


    /// <inheritdoc />
    public delegate void ConnectedEventHandler(object sender, ConnectedEventArgs e);

    /// <inheritdoc />
    public delegate void DisconnectedEventHandler(object sender, DisconnectedEventArgs e);


    /// <summary>
    /// Represents a connection to a RabbitMQ server
    /// </summary>
    public interface IConnection : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Creates a subscriber to consume messages from the bound queues.
        /// </summary>
        /// <param name="startConsuming">If true, the subscriber will start consuming messages immediately. If false, the queues will be
        /// declared but no messages will be consumed yet. Call Resume on the returned ISubscriber to start consuming messages.</param>
        Task<ISubscriber> Subscribe(bool startConsuming = true);


        /// <summary>
        /// Synchronous version of Subscribe.
        /// </summary>
        /// <param name="startConsuming">If true, the subscriber will start consuming messages immediately. If false, the queues will be
        /// declared but no messages will be consumed yet. Call Resume on the returned ISubscriber to start consuming messages.</param>
        ISubscriber SubscribeSync(bool startConsuming = true);


        /// <summary>
        /// Stops the current subscriber.
        /// </summary>
        Task Unsubscribe();


        /// <summary>
        /// Returns an IPublisher implementation for the current connection.
        /// </summary>
        /// <returns></returns>
        IPublisher GetPublisher();


        /// <summary>
        /// Open the connection to RabbitMQ.
        /// </summary>
        /// <remarks>
        /// There is usually no need to call this method manually, the connection will be opened on demand.
        /// </remarks>
        ValueTask Open();


        /// <summary>
        /// Closes the connection to RabbitMQ.
        /// </summary>
        Task Close();


        /// <summary>
        /// Fired when a connection to RabbitMQ has been established.
        /// </summary>
        event ConnectedEventHandler Connected;

        /// <summary>
        /// Fired when the connection to RabbitMQ has been lost.
        /// </summary>
        event DisconnectedEventHandler Disconnected;

        /// <summary>
        /// Fired when the connection to RabbitMQ has been recovered after an unexpected disconnect.
        /// </summary>
        event ConnectedEventHandler Reconnected;

    }
}

using System;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace Tapeti
{
    /// <summary>
    /// Contains information about the established connection.
    /// </summary>
    public class ConnectedEventArgs
    {
        /// <summary>
        /// The connection parameters used to establish the connection.
        /// </summary>
        public TapetiConnectionParams ConnectionParams { get; }

        /// <summary>
        /// The local port for the connection. Useful for identifying the connection in the management interface.
        /// </summary>
        public int LocalPort { get; }


        /// <summary></summary>
        public ConnectedEventArgs(TapetiConnectionParams connectionParams, int localPort)
        {
            ConnectionParams = connectionParams;
            LocalPort = localPort;
        }
    }


    /// <summary>
    /// Contains information about the reason for a lost connection.
    /// </summary>
    public class DisconnectedEventArgs
    {
        /// <summary>
        /// The ReplyCode as indicated by the client library
        /// </summary>
        public ushort ReplyCode { get; }

        /// <summary>
        /// The ReplyText as indicated by the client library
        /// </summary>
        public string ReplyText { get; }


        /// <summary></summary>
        public DisconnectedEventArgs(ushort replyCode, string replyText)
        {
            ReplyCode = replyCode;
            ReplyText = replyText;
        }
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
        /// Returns an IPublisher implementation for the current connection.
        /// </summary>
        /// <returns></returns>
        IPublisher GetPublisher();


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

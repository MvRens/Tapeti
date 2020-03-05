using System;
using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <summary>
    /// Contains information about the connection being established.
    /// </summary>
    public interface IConnectContext
    {
        /// <summary>
        /// The connection parameters used to establish the connection.
        /// </summary>
        TapetiConnectionParams ConnectionParams { get; }

        /// <summary>
        /// Indicates whether this is an automatic reconnect or an initial connection.
        /// </summary>
        bool IsReconnect { get; }
    }


    /// <inheritdoc />
    /// <summary>
    /// Contains information about the failed connection.
    /// </summary>
    public interface IConnectFailedContext : IConnectContext
    {
        /// <summary>
        /// The exception that caused the connection to fail.
        /// </summary>
        Exception Exception { get; }
    }


    /// <inheritdoc />
    /// <summary>
    /// Contains information about the established connection.
    /// </summary>
    public interface IConnectSuccessContext : IConnectContext
    {
        /// <summary>
        /// The local port for the connection. Useful for identifying the connection in the management interface.
        /// </summary>
        int LocalPort { get; }
    }


    /// <summary>
    /// Contains information about the disconnection.
    /// </summary>
    public interface IDisconnectContext
    {
        /// <summary>
        /// The connection parameters used to establish the connection.
        /// </summary>
        TapetiConnectionParams ConnectionParams { get; }

        /// <summary>
        /// The reply code as provided by RabbitMQ, if the connection was closed by a protocol message.
        /// </summary>
        ushort ReplyCode { get; }

        /// <summary>
        /// The reply text as provided by RabbitMQ, if the connection was closed by a protocol message.
        /// </summary>
        string ReplyText { get; }
    }


    /// <summary>
    /// Handles the logging of various events in Tapeti
    /// </summary>
    /// <remarks>
    /// This interface is deliberately specific and typed to allow for structured logging (e.g. Serilog)
    /// instead of only string-based logging without control over the output.
    /// </remarks>
    public interface ILogger
    {
        /// <summary>
        /// Called before a connection to RabbitMQ is attempted.
        /// </summary>
        /// <param name="connectContext">Contains information about the connection being established.</param>
        void Connect(IConnectContext connectContext);

        /// <summary>
        /// Called when the connection has failed.
        /// </summary>
        /// <param name="connectContext">Contains information about the connection that has failed.</param>
        void ConnectFailed(IConnectFailedContext connectContext);

        /// <summary>
        /// Called when a connection to RabbitMQ has been succesfully established.
        /// </summary>
        /// <param name="connectContext">Contains information about the established connection.</param>
        void ConnectSuccess(IConnectSuccessContext connectContext);

        /// <summary>
        /// Called when the connection to RabbitMQ is lost.
        /// </summary>
        /// <param name="disconnectContext">Contains information about the disconnect event.</param>
        void Disconnect(IDisconnectContext disconnectContext);

        /// <summary>
        /// Called when an exception occurs in a consumer.
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="messageContext"></param>
        /// <param name="consumeResult">Indicates the action taken by the exception handler</param>
        void ConsumeException(Exception exception, IMessageContext messageContext, ConsumeResult consumeResult);
    }


    /// <summary>
    /// Optional interface which can be implemented by an ILogger implementation to log all operations
    /// related to declaring queues and bindings.
    /// </summary>
    public interface IBindingLogger : ILogger
    {
        /// <summary>
        /// Called before a queue is declared for durable queues and dynamic queues with a prefix. Called after
        /// a queue is declared for dynamic queues without a name with the queue name as determined by the RabbitMQ server.
        /// Will always be called even if the queue already existed, as that information is not returned by the RabbitMQ server/client.
        /// </summary>
        /// <param name="queueName">The name of the queue that is declared</param>
        /// <param name="durable">Indicates if the queue is durable or dynamic</param>
        /// <param name="passive">Indicates whether the queue was declared as passive (to verify durable queues)</param>
        void QueueDeclare(string queueName, bool durable, bool passive);

        /// <summary>
        /// Called before a binding is added to a queue.
        /// </summary>
        /// <param name="queueName">The name of the queue the binding is created for</param>
        /// <param name="durable">Indicates if the queue is durable or dynamic</param>
        /// <param name="exchange">The exchange for the binding</param>
        /// <param name="routingKey">The routing key for the binding</param>
        void QueueBind(string queueName, bool durable, string exchange, string routingKey);

        /// <summary>
        /// Called before a binding is removed from a durable queue.
        /// </summary>
        /// <param name="queueName">The name of the queue the binding is removed from</param>
        /// <param name="exchange">The exchange of the binding</param>
        /// <param name="routingKey">The routing key of the binding</param>
        void QueueUnbind(string queueName, string exchange, string routingKey);

        /// <summary>
        /// Called before an exchange is declared. Will always be called once for each exchange involved in a dynamic queue,
        /// durable queue with auto-declare bindings enabled or published messages, even if the exchange already existed.
        /// </summary>
        /// <param name="exchange">The name of the exchange that is declared</param>
        void ExchangeDeclare(string exchange);

        /// <summary>
        /// Called when a queue is determined to be obsolete.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="deleted">True if the queue was empty and has been deleted, false if there are still messages to process</param>
        /// <param name="messageCount">If deleted, the number of messages purged, otherwise the number of messages still in the queue</param>
        void QueueObsolete(string queueName, bool deleted, uint messageCount);
    }
}

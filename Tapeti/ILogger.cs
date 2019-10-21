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

        /// <summary>
        /// Called when a queue is determined to be obsolete.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="deleted">True if the queue was empty and has been deleted, false if there are still messages to process</param>
        /// <param name="messageCount">If deleted, the number of messages purged, otherwise the number of messages still in the queue</param>
        void QueueObsolete(string queueName, bool deleted, uint messageCount);
    }
}

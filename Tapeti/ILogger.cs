using System;
using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
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
        /// <param name="connectionParams"></param>
        /// <param name="isReconnect">Indicates whether this is the initial connection or a reconnect</param>
        void Connect(TapetiConnectionParams connectionParams, bool isReconnect);

        /// <summary>
        /// Called when the connection has failed or is lost.
        /// </summary>
        /// <param name="connectionParams"></param>
        /// <param name="exception"></param>
        void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception);

        /// <summary>
        /// Called when a connection to RabbitMQ has been succesfully established.
        /// </summary>
        /// <param name="connectionParams"></param>
        /// <param name="isReconnect">Indicates whether this is the initial connection or a reconnect</param>
        void ConnectSuccess(TapetiConnectionParams connectionParams, bool isReconnect);

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

using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Tapeti.Config;
using Tapeti.Config.Annotations;
using Tapeti.Connection;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace Tapeti;

/// <summary>
/// Contains information about the connection being established.
/// </summary>
public abstract class ConnectContext
{
    /// <summary>
    /// The connection parameters used to establish the connection.
    /// </summary>
    public required TapetiConnectionParams ConnectionParams { get; init; }

    /// <summary>
    /// Indicates whether this is an automatic reconnect or an initial connection.
    /// </summary>
    public required bool IsReconnect { get; init; }
}


/// <summary>
/// Contains information about the failed connection.
/// </summary>
public class ConnectFailedContext : ConnectContext
{
    /// <summary>
    /// The exception that caused the connection to fail.
    /// </summary>
    public required Exception? Exception { get; init; }
}


/// <summary>
/// Contains information about the established connection.
/// </summary>
public class ConnectSuccessContext : ConnectContext
{
    /// <summary>
    /// The local port for the connection. Useful for identifying the connection in the management interface.
    /// </summary>
    public required int LocalPort { get; init; }
}


/// <summary>
/// Contains information about the disconnection.
/// </summary>
public class DisconnectContext
{
    /// <summary>
    /// The connection parameters used to establish the connection.
    /// </summary>
    public required TapetiConnectionParams ConnectionParams { get; init; }

    /// <summary>
    /// The reply code as provided by RabbitMQ, if the connection was closed by a protocol message.
    /// </summary>
    public required ushort ReplyCode { get; init; }

    /// <summary>
    /// The reply text as provided by RabbitMQ, if the connection was closed by a protocol message.
    /// </summary>
    public required string ReplyText { get; init; }
}


/// <summary>
/// Indicates how Tapeti uses the channel.
/// </summary>
public enum ChannelType
{
    /// <summary>
    /// This channel is the default channel for publishing messages.
    /// </summary>
    PublishDefault,

    /// <summary>
    /// This channel is the default channel for consumers.
    /// </summary>
    ConsumeDefault,

    /// <summary>
    /// This channel is for consumers marked with the <see cref="DedicatedChannelAttribute"/>.
    /// </summary>
    ConsumeDedicated
}


/// <summary>
/// Contains information about the created channel.
/// </summary>
public class ChannelCreatedContext
{
    /// <inheritdoc cref="Tapeti.ChannelType"/>
    public required ChannelType ChannelType { get; init; }

    /// <summary>
    /// A unique number associated with the connection the channel was created on.
    /// </summary>
    public required long ConnectionReference { get; init; }

    /// <summary>
    /// The number of this channel, unique to the connection.
    /// </summary>
    public required long ChannelNumber { get; init; }

    /// <summary>
    /// Indicates whether this is an automatic recreate or an initial creation.
    /// </summary>
    public required bool IsRecreate { get; init; }
}


/// <summary>
/// Contains information related to the channel shutdown event.
/// </summary>
public class ChannelShutdownContext
{
    /// <inheritdoc cref="Tapeti.ChannelType"/>
    public required ChannelType ChannelType { get; init; }

    /// <summary>
    /// A unique number associated with the connection the channel was created on.
    /// </summary>
    public required long ConnectionReference { get; init; }

    /// <summary>
    /// The number of this channel, unique to the connection it was created on.
    /// </summary>
    public required long ChannelNumber { get; init; }

    /// <inheritdoc cref="ShutdownEventArgs.Initiator"/>
    public required ShutdownInitiator Initiator { get; init; }

    /// <inheritdoc cref="ShutdownEventArgs.ReplyCode"/>
    public required ushort ReplyCode { get; init; }

    /// <inheritdoc cref="ShutdownEventArgs.ReplyText"/>
    public required string ReplyText { get; init; }
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
    void Connect(ConnectContext connectContext);

    /// <summary>
    /// Called when the connection has failed.
    /// </summary>
    /// <param name="connectContext">Contains information about the connection that has failed.</param>
    void ConnectFailed(ConnectFailedContext connectContext);

    /// <summary>
    /// Called when a connection to RabbitMQ has been succesfully established.
    /// </summary>
    /// <param name="connectContext">Contains information about the established connection.</param>
    void ConnectSuccess(ConnectSuccessContext connectContext);

    /// <summary>
    /// Called when the connection to RabbitMQ is lost.
    /// </summary>
    /// <param name="disconnectContext">Contains information about the disconnect event.</param>
    void Disconnect(DisconnectContext disconnectContext);

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
    /// Called when a durable queue would be declared but already exists with incompatible x-arguments. The existing
    /// queue will be consumed without declaring to prevent errors during startup. This is used for compatibility with existing queues
    /// not declared by Tapeti.
    /// If the queue already exists but should be compatible QueueDeclare will be called instead.
    /// </summary>
    /// <param name="queueName">The name of the queue that is declared</param>
    /// <param name="existingArguments">The x-arguments of the existing queue</param>
    /// <param name="arguments">The x-arguments of the queue that would be declared</param>
    void QueueExistsWarning(string queueName, IRabbitMQArguments? existingArguments, IRabbitMQArguments? arguments);

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


/// <summary>
/// Optional interface which can be implemented by an ILogger implementation to log all operations
/// related to channels.
/// </summary>
public interface IChannelLogger : ILogger
{
    /// <summary>
    /// Called after a channel has been created.
    /// </summary>
    /// <param name="context">Contains information about the created channel.</param>
    void ChannelCreated(ChannelCreatedContext context);

    /// <summary>
    /// Called when a channel is shut down unexpectedly.
    /// </summary>
    /// <param name="context">Contains information about the shutdown event.</param>
    void ChannelShutdown(ChannelShutdownContext context);
}

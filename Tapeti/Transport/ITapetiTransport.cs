using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Connection;

namespace Tapeti.Transport;


/// <summary>
/// Factory for <see cref="ITapetiTransport"/>.
/// </summary>
public interface ITapetiTransportFactory
{
    /// <summary>
    /// Create a new <see cref="ITapetiTransport"/> for the specified connection parameters.
    /// </summary>
    ITapetiTransport Create(TapetiConnectionParams connectionParams);
}


/// <summary>
/// A thin wrapper around the official RabbitMQ Client.
/// </summary>
public interface ITapetiTransport : IAsyncDisposable
{
    /// <summary>
    /// Open the connection to RabbitMQ.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="CreateChannel"/> will also open the connection if required.
    /// </remarks>
    ValueTask Open();


    /// <summary>
    /// Close the connection to RabbitMQ.
    /// </summary>
    ValueTask Close();


    /// <summary>
    /// Determines if the connection is currently in the process of closing.
    /// </summary>
    bool IsClosing { get; }


    /// <summary>
    /// Creates a new channel on the current connection.
    /// </summary>
    /// <remarks>
    /// Will open the connection if required.
    /// </remarks>
    Task<ITapetiTransportChannel> CreateChannel(TapetiChannelOptions options);


    /// <summary>
    /// Attaches an observer to this transport to be notified of connection changes.
    /// </summary>
    void AttachObserver(ITapetiTransportObserver observer);
}


/// <summary>
/// Describes the options used for creating a new channel.
/// </summary>
public class TapetiChannelOptions
{
    /// <summary>
    /// The channel type, for logging purposes.
    /// </summary>
    public required ChannelType ChannelType { get; init; }

    /// <summary>
    /// Determines if publisher confirmations are enabled.
    /// </summary>
    public required bool PublisherConfirmationsEnabled { get; init; }

    /// <summary>
    /// The amount of message to prefetch. See http://www.rabbitmq.com/consumer-prefetch.html for more information.
    ///
    /// If set to 0, no limit will be applied.
    /// </summary>
    public required ushort PrefetchCount { get; init; }
}


/// <summary>
/// A thin wrapper around a RabbitMQ Client channel.
/// </summary>
public interface ITapetiTransportChannel
{
    /// <summary>
    /// A unique number associated with the connection the channel was created on.
    /// </summary>
    public long ConnectionReference { get; }

    /// <summary>
    /// The number of this channel, unique to the connection.
    /// </summary>
    public long ChannelNumber { get; }

    /// <summary>
    /// Determines if this channel is still open.
    /// </summary>
    public bool IsOpen { get; }

    /// <summary>
    /// A cancellation token which is cancelled when the channel is closed.
    /// </summary>
    public CancellationToken ChannelClosed { get; }

    /// <summary>
    /// Attaches an observer to this channel to be notified of status changes.
    /// </summary>
    void AttachObserver(ITapetiTransportChannelObserver observer);

    /// <summary>
    /// Starts a consumer for the specified queue, using the provided bindings to handle messages.
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="consumer">The consumer implementation which will receive the messages from the queue</param>
    /// <param name="messageHandlerTracker"></param>
    /// <returns>A representation of the consumer and channel.</returns>
    Task<ITapetiTransportConsumer?> Consume(string queueName, IConsumer consumer, IMessageHandlerTracker messageHandlerTracker);

    /// <summary>
    /// Publishes a message. The exchange and routing key are determined by the registered strategies.
    /// </summary>
    /// <param name="body">The raw message data to publish</param>
    /// <param name="properties">Metadata to include in the message</param>
    /// <param name="exchange">The exchange to publish the message to, or empty to send it directly to a queue</param>
    /// <param name="routingKey">The routing key for the message, or queue name if exchange is empty</param>
    /// <param name="mandatory">If true, an exception will be raised if the message can not be delivered to at least one queue</param>
    Task Publish(byte[] body, IMessageProperties properties, string? exchange, string routingKey, bool mandatory);

    /// <summary>
    /// Creates a durable queue if it does not already exist, and updates the bindings.
    /// </summary>
    /// <param name="queueName">The name of the queue to create</param>
    /// <param name="bindings">A list of bindings. Any bindings already on the queue which are not in this list will be removed</param>
    /// <param name="arguments">Optional arguments</param>
    /// <param name="cancellationToken">Cancelled when the connection is lost</param>
    Task DurableQueueDeclare(string queueName, IEnumerable<QueueBinding> bindings, IRabbitMQArguments? arguments, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a durable queue exists. Will raise an exception if it does not.
    /// </summary>
    /// <param name="queueName">The name of the queue to verify</param>
    /// <param name="arguments">Optional arguments</param>
    /// <param name="cancellationToken">Cancelled when the connection is lost</param>
    Task DurableQueueVerify(string queueName, IRabbitMQArguments? arguments, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a durable queue.
    /// </summary>
    /// <param name="queueName">The name of the queue to delete</param>
    /// <param name="onlyIfEmpty">If true, the queue will only be deleted if it is empty otherwise all bindings will be removed. If false, the queue is deleted even if there are queued messages.</param>
    /// <param name="cancellationToken">Cancelled when the connection is lost</param>
    Task DurableQueueDelete(string queueName, bool onlyIfEmpty, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a dynamic queue.
    /// </summary>
    /// <param name="queuePrefix">An optional prefix for the dynamic queue's name. If not provided, RabbitMQ's default logic will be used to create an amq.gen queue.</param>
    /// <param name="arguments">Optional arguments</param>
    /// <param name="cancellationToken">Cancelled when the connection is lost</param>
    Task<string> DynamicQueueDeclare(string? queuePrefix, IRabbitMQArguments? arguments, CancellationToken cancellationToken);

    /// <summary>
    /// Add a binding to a dynamic queue.
    /// </summary>
    /// <param name="queueName">The name of the dynamic queue previously created using DynamicQueueDeclare</param>
    /// <param name="binding">The binding to add to the dynamic queue</param>
    /// <param name="cancellationToken">Cancelled when the connection is lost</param>
    Task DynamicQueueBind(string queueName, QueueBinding binding, CancellationToken cancellationToken);
}



/// <summary>
/// Represents a consumer for a specific connection and channel.
/// </summary>
public interface ITapetiTransportConsumer
{
    /// <summary>
    /// Stops the consumer.
    /// </summary>
    Task Cancel();
}


/// <summary>
/// Received updates on the status of the connection.
/// </summary>
public interface ITapetiTransportObserver
{
    /// <summary>
    /// Called when a connection to RabbitMQ has been established.
    /// </summary>
    void Connected(ConnectedEventArgs e);


    /// <summary>
    /// Called when the connection to RabbitMQ has been recovered after an unexpected disconnect.
    /// </summary>
    void Reconnected(ConnectedEventArgs e);


    /// <summary>
    /// Called when the connection to RabbitMQ has been lost.
    /// </summary>
    void Disconnected(DisconnectedEventArgs e);
}



/// <summary>
/// Received updates on the status of the channel.
/// </summary>
public interface ITapetiTransportChannelObserver
{
    /// <summary>
    /// Called when a RabbitMQ Client channel is shut down.
    /// </summary>
    ValueTask OnShutdown(ChannelShutdownEventArgs e);
}

using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Transport;

namespace Tapeti.Connection;



/// <summary>
/// Represents a resilient RabbitMQ Client channel, and it's associated single-thread task queue.
/// Access to the ITapetiTransportChannel is limited by design to enforce this relationship.
/// </summary>
public interface ITapetiChannel
{
    /// <summary>
    /// Places the operation to be performed on the channel in the task queue to ensure single-thread access.
    /// </summary>
    Task Enqueue(Func<ITapetiTransportChannel, Task> operation);


    /// <summary>
    /// Places the operation to be performed on the channel in the task queue to ensure single-thread access,
    /// and returns its result.
    /// </summary>
    Task<T> Enqueue<T>(Func<ITapetiTransportChannel, Task<T>> operation);


    /// <summary>
    /// Attaches an observer to this channel to be notified of status changes.
    /// </summary>
    void AttachObserver(ITapetiChannelObserver observer);
}



/// <summary>
/// Received updates on the status of the channel.
/// </summary>
public interface ITapetiChannelObserver
{
    /// <summary>
    /// Called when the channel on the transport layer has been shutdown, before the channel is recreated.
    /// </summary>
    ValueTask OnShutdown(ChannelShutdownEventArgs e);


    /// <summary>
    /// Called when the channel on the transport layer has been shutdown and successfully recreated.
    /// </summary>
    /// <remarks>
    /// If the recreation was triggered by an operation on the channel, this event will fire
    /// before the operation is performed.
    /// </remarks>
    ValueTask OnRecreated(ITapetiTransportChannel newChannel);
}


/// <summary>
/// Contains information about the channel shutdown.
/// </summary>
public class ChannelShutdownEventArgs
{
    /// <summary>
    /// Determines if the connection is closing by request.
    /// </summary>
    public required bool IsClosing { get; init; }

    /// <inheritdoc cref="ShutdownInitiator"/>
    public required ShutdownInitiator Initiator { get; init; }

    /// <summary>
    /// The reply code as provided by RabbitMQ, if the connection was closed by a protocol message.
    /// </summary>
    public required ushort? ReplyCode { get; init; }

    /// <summary>
    /// The reply text as provided by RabbitMQ, if the connection was closed by a protocol message.
    /// </summary>
    public required string? ReplyText { get; init; }
}

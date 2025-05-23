using System;
using System.Threading.Tasks;
using Tapeti.Transport;

namespace Tapeti.Connection;


/// <summary>
/// Called when the channel on the transport layer has been shutdown and successfully recreated.
/// </summary>
/// <remarks>
/// If the recreate was triggered by an operation on the channel, this event will fire
/// before the operation is performed.
/// </remarks>
public delegate ValueTask TapetiChannelRecreatedEvent(ITapetiTransportChannel transportChannel);


/// <summary>
/// Represents a resilient RabbitMQ Client channel, and it's associated single-thread task queue.
/// Access to the ITapetiTransportChannel is limited by design to enforce this relationship.
/// </summary>
public interface ITapetiChannel
{
    /// <inheritdoc cref="TapetiChannelRecreatedEvent"/>
    TapetiChannelRecreatedEvent? OnRecreated { get; set; }


    /// <summary>
    /// Places the operation to be performed on the channel in the task queue to ensure single-thread access.
    /// </summary>
    Task Enqueue(Func<ITapetiTransportChannel, Task> operation);


    /// <summary>
    /// Places the operation to be performed on the channel in the task queue to ensure single-thread access,
    /// and returns its result.
    /// </summary>
    Task<T> Enqueue<T>(Func<ITapetiTransportChannel, Task<T>> operation);
}

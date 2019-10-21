using System;

namespace Tapeti.Flow.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// Marks a message handler as a response message handler which continues a Tapeti Flow.
    /// The method only receives direct messages, and does not create a routing key based binding to the queue.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ContinuationAttribute : Attribute
    {
    }
}

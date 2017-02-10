using System;

namespace Tapeti.Annotations
{
    /// <summary>
    /// Creates a non-durable auto-delete queue to receive messages. Can be used
    /// on an entire MessageController class or on individual methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DynamicQueueAttribute : Attribute
    {
    }
}

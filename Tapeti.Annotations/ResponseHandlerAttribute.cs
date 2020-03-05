using System;

namespace Tapeti.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// Indicates that the method only handles response messages which are sent directly
    /// to the queue. No binding will be created.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ResponseHandlerAttribute : Attribute
    {
    }
}

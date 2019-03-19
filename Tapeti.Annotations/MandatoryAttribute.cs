using System;

namespace Tapeti.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// Can be attached to a message class to specify that delivery to a queue must be guaranteed.
    /// Publish will fail if no queues bind to the routing key. Publisher confirms must be enabled
    /// on the connection for this to work (enabled by default).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MandatoryAttribute : Attribute
    {
    }
}

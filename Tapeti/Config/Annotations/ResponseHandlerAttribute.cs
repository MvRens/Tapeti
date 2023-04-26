using JetBrains.Annotations;
using System;

namespace Tapeti.Config.Annotations
{
    /// <summary>
    /// Indicates that the method only handles response messages which are sent directly
    /// to the queue. No binding will be created.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [PublicAPI]
    public class ResponseHandlerAttribute : Attribute
    {
    }
}

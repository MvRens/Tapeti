using System;
using JetBrains.Annotations;

namespace Tapeti.Config.Annotations
{
    /// <summary>
    /// Indicates that the method is not a message handler and should not be bound by Tapeti.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [PublicAPI]
    public class NoBindingAttribute : Attribute
    {
    }
}

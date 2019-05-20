using System;
using JetBrains.Annotations;

namespace Tapeti.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// This attribute does nothing in runtime and is not required. It is only used as
    /// a hint to ReSharper, and maybe developers as well, to indicate the method is
    /// indeed used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    public class MessageHandlerAttribute : Attribute
    {
    }
}

using System;
using JetBrains.Annotations;

namespace Tapeti.Annotations
{
    /// <inheritdoc />
    /// <summary>
    /// Attaching this attribute to a class includes it in the auto-discovery of message controllers
    /// when using the RegisterAllControllers method. It is not required when manually registering a controller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
    public class MessageControllerAttribute : Attribute
    {
    }
}

using System;
using JetBrains.Annotations;

namespace Tapeti.Config.Annotations
{
    /// <summary>
    /// Attaching this attribute to a class includes it in the auto-discovery of message controllers
    /// when using the RegisterAllControllers method. It is not required when manually registering a controller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
    [PublicAPI]
    public class MessageControllerAttribute : Attribute
    {
    }
}

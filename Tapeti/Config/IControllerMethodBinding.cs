using System;
using System.Reflection;

namespace Tapeti.Config
{
    /// <summary>
    /// Represents a binding to a method in a controller class to handle incoming messages.
    /// </summary>
    public interface IControllerMethodBinding : IBinding
    {
        /// <summary>
        /// The controller class.
        /// </summary>
        Type Controller { get; }

        /// <summary>
        /// The method on the Controller class to which this binding is bound.
        /// </summary>
        MethodInfo Method { get; }
    }
}

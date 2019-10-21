using System.Collections.Generic;

namespace Tapeti.Config
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a way for Tapeti extensions to register custom bindings.
    /// </summary>
    public interface ITapetiExtensionBinding : ITapetiExtension
    {
        /// <summary>
        /// Produces a list of bindings to be registered.
        /// </summary>
        /// <param name="dependencyResolver"></param>
        /// <returns>A list of bindings or null if no bindings need to be registered</returns>
        IEnumerable<IBinding> GetBindings(IDependencyResolver dependencyResolver);
    }
}
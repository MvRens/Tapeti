using System.Collections.Generic;

namespace Tapeti.Config
{
    /// <summary>
    /// A bundling mechanism for Tapeti extension packages. Allows the calling application to
    /// pass all the necessary components to TapetiConfig.Use in one call.
    /// </summary>
    public interface ITapetiExtension
    {
        /// <summary>
        /// Allows the extension to register default implementations into the IoC container.
        /// </summary>
        /// <param name="container"></param>
        void RegisterDefaults(IDependencyContainer container);

        /// <summary>
        /// Produces a list of middleware implementations to be passed to the TapetiConfig.Use method.
        /// </summary>
        /// <param name="dependencyResolver"></param>
        /// <returns>A list of middleware implementations or null if no middleware needs to be registered</returns>
        IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver);
    }
}

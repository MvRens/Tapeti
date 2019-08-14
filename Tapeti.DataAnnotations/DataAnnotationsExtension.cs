using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    /// <inheritdoc />
    /// <summary>
    /// Provides the DataAnnotations validation middleware.
    /// </summary>
    public class DataAnnotationsExtension : ITapetiExtension
    {
        /// <inheritdoc />
        public void RegisterDefaults(IDependencyContainer container)
        {
        }

        /// <inheritdoc />
        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return new object[]
            {
                new DataAnnotationsMessageMiddleware(), 
                new DataAnnotationsPublishMiddleware()
            };
        }
    }
}

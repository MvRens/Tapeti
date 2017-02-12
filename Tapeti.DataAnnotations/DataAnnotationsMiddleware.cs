using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.DataAnnotations
{
    public class DataAnnotationsMiddleware : ITapetiExtension
    {
        public void RegisterDefaults(IDependencyContainer container)
        {
        }

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

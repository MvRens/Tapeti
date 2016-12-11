using System.Collections.Generic;

namespace Tapeti.Config
{
    public interface IMiddlewareBundle
    {
        IEnumerable<object> GetContents(IDependencyResolver dependencyResolver);
    }
}

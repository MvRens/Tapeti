using System.Collections.Generic;

namespace Tapeti.Config
{
    public interface ITapetiExtentionBinding
    {
        IEnumerable<ICustomBinding> GetBindings(IDependencyResolver dependencyResolver);

    }
}
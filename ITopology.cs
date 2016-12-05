using System.Collections.Generic;

namespace Tapeti
{
    public interface ITopology
    {
        IEnumerable<IQueue> Queues();
    }


    public interface IQueue
    {
        IEnumerable<IBinding> Bindings();
    }


    public interface IBinding
    {
    }
}

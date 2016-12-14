using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IConfig
    {
        string Exchange { get; }
        IDependencyResolver DependencyResolver { get; }
        IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }
        IEnumerable<IQueue> Queues { get; }
    }


    public interface IQueue
    {
        bool Dynamic { get; }
        string Name { get; }

        IEnumerable<IBinding> Bindings { get; }
    }


    public interface IBinding
    {
        Type Controller { get; }
        MethodInfo Method { get; }
        Type MessageClass { get; }

        IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }

        bool Accept(object message);
        Task<object> Invoke(IMessageContext context, object message);
    }
}

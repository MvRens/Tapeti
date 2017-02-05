using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IConfig
    {
        string SubscribeExchange { get; }
        IDependencyResolver DependencyResolver { get; }
        IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }
        IEnumerable<IQueue> Queues { get; }

        IBinding GetBinding(Delegate method);
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
        string QueueName { get; }

        IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }
        IReadOnlyList<IBindingFilter> BindingFilters { get; }

        Task<bool> Accept(IMessageContext context, object message);
        Task Invoke(IMessageContext context, object message);
    }


    public interface IDynamicQueueBinding : IBinding
    {
        void SetQueueName(string queueName);
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface IConfig
    {
        IDependencyResolver DependencyResolver { get; }
        IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }
        IReadOnlyList<ICleanupMiddleware> CleanupMiddleware { get; }
        IReadOnlyList<IPublishMiddleware> PublishMiddleware { get; }
        IEnumerable<IQueue> Queues { get; }

        IBinding GetBinding(Delegate method);
    }


    public interface IQueue
    {
        bool Dynamic { get; }
        string Name { get; }

        IEnumerable<IBinding> Bindings { get; }
    }


    public interface IDynamicQueue : IQueue
    {
        void SetName(string name);
    }


    public interface IBinding
    {
        Type Controller { get; }
        MethodInfo Method { get; }
        Type MessageClass { get; }
        string QueueName { get; }
        QueueBindingMode QueueBindingMode { get; set; }

        IReadOnlyList<IMessageFilterMiddleware> MessageFilterMiddleware { get; }
        IReadOnlyList<IMessageMiddleware> MessageMiddleware { get; }

        bool Accept(IMessageContext context, object message);
        Task Invoke(IMessageContext context, object message);
    }


    public interface IBuildBinding : IBinding
    {
        void SetQueueName(string queueName);
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    public interface ICustomBinding
    {
        Type Controller { get; }

        MethodInfo Method { get; }

        QueueBindingMode QueueBindingMode { get; }

        string StaticQueueName { get; }

        string DynamicQueuePrefix { get; }

        Type MessageClass { get; } // Needed to get routing key information when QueueBindingMode = RoutingKey

        bool Accept(Type messageClass);

        bool Accept(IMessageContext context, object message);

        Task Invoke(IMessageContext context, object message);

        void SetQueueName(string queueName);
    }
}

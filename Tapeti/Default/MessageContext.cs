using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class MessageContext : IMessageContext
    {
        public IDependencyResolver DependencyResolver { get; set; }

        public object Controller { get; set; }
        public IBinding Binding { get; set; }

        public string Queue { get; set; }
        public string RoutingKey { get; set; }
        public object Message { get; set; }
        public IBasicProperties Properties { get; set; }

        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();


        public void Dispose()
        {
            foreach (var value in Items.Values)
                (value as IDisposable)?.Dispose();
        }
    }
}

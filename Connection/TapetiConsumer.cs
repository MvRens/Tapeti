using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Connection
{
    public class TapetiConsumer : DefaultBasicConsumer
    {
        private readonly TapetiWorker worker;
        private readonly IDependencyResolver dependencyResolver;
        private readonly IReadOnlyList<IMessageMiddleware> messageMiddleware;
        private readonly List<IBinding> bindings;


        public TapetiConsumer(TapetiWorker worker, IDependencyResolver dependencyResolver, IEnumerable<IBinding> bindings, IReadOnlyList<IMessageMiddleware> messageMiddleware)
        {
            this.worker = worker;
            this.dependencyResolver = dependencyResolver;
            this.messageMiddleware = messageMiddleware;
            this.bindings = bindings.ToList();
        }


        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IBasicProperties properties, byte[] body)
        {
            try
            {
                var message = dependencyResolver.Resolve<IMessageSerializer>().Deserialize(body, properties);
                if (message == null)
                    throw new ArgumentException("Empty message");

                var validMessageType = false;
                foreach (var binding in bindings.Where(b => b.Accept(message)))
                {
                    var context = new MessageContext
                    {
                        DependencyResolver = dependencyResolver,
                        Controller = dependencyResolver.Resolve(binding.Controller),
                        Message = message,
                        Properties = properties
                    };

                    MiddlewareHelper.GoAsync(binding.MessageMiddleware != null ? messageMiddleware.Concat(binding.MessageMiddleware).ToList() : messageMiddleware, 
                        async (handler, next) => await handler.Handle(context, next),
                        async () =>
                        {
                            var result = binding.Invoke(context, message).Result;
                            if (result != null)
                                await worker.Publish(result, null);
                        }
                        ).Wait();

                    validMessageType = true;
                }

                if (!validMessageType)
                    throw new ArgumentException($"Unsupported message type: {message.GetType().FullName}");

                worker.Respond(deliveryTag, ConsumeResponse.Ack);
            }
            catch (Exception)
            {
                worker.Respond(deliveryTag, ConsumeResponse.Requeue);
                throw;
            }
        }


        protected class MessageContext : IMessageContext
        {
            public IDependencyResolver DependencyResolver { get; set; }

            public object Controller { get; set; }
            public object Message { get; set;  }
            public IBasicProperties Properties { get; set;  }

            public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
        }
    }
}

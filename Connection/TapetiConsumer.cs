using System;
using System.Collections.Generic;
using System.Linq;
using RabbitMQ.Client;
using Tapeti.Config;
using Tapeti.Helpers;

namespace Tapeti.Connection
{
    public class TapetiConsumer : DefaultBasicConsumer
    {
        private readonly TapetiWorker worker;
        private readonly string queueName;
        private readonly IDependencyResolver dependencyResolver;
        private readonly IReadOnlyList<IMessageMiddleware> messageMiddleware;
        private readonly List<IBinding> bindings;


        public TapetiConsumer(TapetiWorker worker, string queueName, IDependencyResolver dependencyResolver, IEnumerable<IBinding> bindings, IReadOnlyList<IMessageMiddleware> messageMiddleware)
        {
            this.worker = worker;
            this.queueName = queueName;
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
                    using (var context = new MessageContext
                    {
                        DependencyResolver = dependencyResolver,
                        Controller = dependencyResolver.Resolve(binding.Controller),
                        Queue = queueName,
                        RoutingKey = routingKey,
                        Message = message,
                        Properties = properties
                    })
                    {
                        // ReSharper disable AccessToDisposedClosure - MiddlewareHelper will not keep a reference to the lambdas
                        MiddlewareHelper.GoAsync(
                            binding.MessageMiddleware != null
                                ? messageMiddleware.Concat(binding.MessageMiddleware).ToList()
                                : messageMiddleware,
                            async (handler, next) => await handler.Handle(context, next),
                            async () =>
                            {
                                var result = binding.Invoke(context, message).Result;

                                // TODO change to result handler
                                if (result != null)
                                    await worker.Publish(result, null);
                            }
                        ).Wait();
                        // ReSharper restore AccessToDisposedClosure
                    }

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

            public string Queue { get; set; }
            public string RoutingKey { get; set; }
            public object Message { get; set;  }
            public IBasicProperties Properties { get; set;  }

            public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();


            public void Dispose()
            {
                foreach (var value in Items.Values)
                    (value as IDisposable)?.Dispose();
            }
        }
    }
}

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

                var handled = false;
                foreach (var binding in bindings.Where(b => b.Accept(message)))
                {
                    var context = new MessageContext
                    {
                        Controller = dependencyResolver.Resolve(binding.Controller),
                        Message = message
                    };

                    MiddlewareHelper.Go(messageMiddleware, (handler, next) => handler.Handle(context, next));

                    var result = binding.Invoke(context, message).Result;
                    if (result != null)
                        worker.Publish(result);

                    handled = true;
                }

                if (!handled)
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
            public object Controller { get; set; }
            public object Message { get; set;  }
            public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
        }
    }
}

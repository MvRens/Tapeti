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
        private readonly IExceptionStrategy exceptionStrategy;


        public TapetiConsumer(TapetiWorker worker, string queueName, IDependencyResolver dependencyResolver, IEnumerable<IBinding> bindings, IReadOnlyList<IMessageMiddleware> messageMiddleware)
        {
            this.worker = worker;
            this.queueName = queueName;
            this.dependencyResolver = dependencyResolver;
            this.messageMiddleware = messageMiddleware;
            this.bindings = bindings.ToList();

            exceptionStrategy = dependencyResolver.Resolve<IExceptionStrategy>();
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

                using (var context = new MessageContext
                {
                    DependencyResolver = dependencyResolver,
                    Queue = queueName,
                    RoutingKey = routingKey,
                    Message = message,
                    Properties = properties
                })
                {
                    try
                    {
                        foreach (var binding in bindings)
                        {
                            if (!binding.Accept(context, message).Result)
                                continue;

                            context.Controller = dependencyResolver.Resolve(binding.Controller);
                            context.Binding = binding;

                            // ReSharper disable AccessToDisposedClosure - MiddlewareHelper will not keep a reference to the lambdas
                            MiddlewareHelper.GoAsync(
                                binding.MessageMiddleware != null
                                    ? messageMiddleware.Concat(binding.MessageMiddleware).ToList()
                                    : messageMiddleware,
                                async (handler, next) => await handler.Handle(context, next),
                                () => binding.Invoke(context, message)
                            ).Wait();
                            // ReSharper restore AccessToDisposedClosure

                            validMessageType = true;
                        }

                        if (!validMessageType)
                            throw new ArgumentException($"Unsupported message type: {message.GetType().FullName}");
                    }
                    catch (Exception e)
                    {
                        worker.Respond(deliveryTag, exceptionStrategy.HandleException(context, UnwrapException(e)));
                    }
                }

                worker.Respond(deliveryTag, ConsumeResponse.Ack);
            }
            catch (Exception e)
            {
                worker.Respond(deliveryTag, exceptionStrategy.HandleException(null, UnwrapException(e)));
            }
        }


        private static Exception UnwrapException(Exception exception)
        {
            // In async/await style code this is handled similarly. For synchronous
            // code using Tasks we have to unwrap these ourselves to get the proper
            // exception directly instead of "Errors occured". We might lose
            // some stack traces in the process though.
            var aggregateException = exception as AggregateException;
            if (aggregateException != null && aggregateException.InnerExceptions.Count == 1)
                throw aggregateException.InnerExceptions[0];

            return UnwrapException(exception);
        }


        protected class MessageContext : IMessageContext
        {
            public IDependencyResolver DependencyResolver { get; set; }

            public object Controller { get; set; }
            public IBinding Binding { get; set; }

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

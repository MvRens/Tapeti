using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using RabbitMQ.Client;
using Tapeti.Config;
using Tapeti.Default;
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
            ExceptionDispatchInfo exception = null;
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
                            if (!binding.Accept(context, message))
                                continue;

                            context.Binding = binding;

                            // ReSharper disable AccessToDisposedClosure - MiddlewareHelper will not keep a reference to the lambdas
                            MiddlewareHelper.GoAsync(
                                binding.MessageFilterMiddleware,
                                async (handler, next) => await handler.Handle(context, next),
                                async () =>
                                {
                                    context.Controller = dependencyResolver.Resolve(binding.Controller);

                                    await MiddlewareHelper.GoAsync(
                                        binding.MessageMiddleware != null
                                            ? messageMiddleware.Concat(binding.MessageMiddleware).ToList()
                                            : messageMiddleware,
                                        async (handler, next) => await handler.Handle(context, next),
                                        () => binding.Invoke(context, message)
                                    );
                                }).Wait();
                            // ReSharper restore AccessToDisposedClosure

                            validMessageType = true;
                        }

                        if (!validMessageType)
                            throw new ArgumentException($"Unsupported message type: {message.GetType().FullName}");

                        worker.Respond(deliveryTag, ConsumeResponse.Ack);
                    }
                    catch (Exception e)
                    {
                        exception = ExceptionDispatchInfo.Capture(UnwrapException(e));
                        worker.Respond(deliveryTag, exceptionStrategy.HandleException(context, exception.SourceException));
                    }
                }
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(UnwrapException(e));
                worker.Respond(deliveryTag, exceptionStrategy.HandleException(null, exception.SourceException));
            }

            exception?.Throw();
        }


        private static Exception UnwrapException(Exception exception)
        {
            // In async/await style code this is handled similarly. For synchronous
            // code using Tasks we have to unwrap these ourselves to get the proper
            // exception directly instead of "Errors occured". We might lose
            // some stack traces in the process though.
            while (true)
            {
                var aggregateException = exception as AggregateException;
                if (aggregateException == null || aggregateException.InnerExceptions.Count != 1)
                    return exception;

                exception = aggregateException.InnerExceptions[0];
            }
        }
    }
}

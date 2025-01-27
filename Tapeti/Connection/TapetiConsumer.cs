using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tapeti.Config;
using Tapeti.Default;
using System.Threading.Tasks;
using Tapeti.Helpers;

namespace Tapeti.Connection
{
    /// <summary>
    /// Implements a RabbitMQ consumer to pass messages to the Tapeti middleware.
    /// </summary>
    internal class TapetiConsumer : IConsumer
    {
        private readonly CancellationToken cancellationToken;
        private readonly ITapetiConfig config;
        private readonly string queueName;
        private readonly List<IBinding> bindings;

        private readonly ILogger logger;
        private readonly IExceptionStrategy exceptionStrategy;
        private readonly IMessageSerializer messageSerializer;


        public TapetiConsumer(CancellationToken cancellationToken, ITapetiConfig config, string queueName, IEnumerable<IBinding> bindings)
        {
            this.cancellationToken = cancellationToken;
            this.config = config;
            this.queueName = queueName;
            this.bindings = bindings.ToList();

            logger = config.DependencyResolver.Resolve<ILogger>();
            exceptionStrategy = config.DependencyResolver.Resolve<IExceptionStrategy>();
            messageSerializer = config.DependencyResolver.Resolve<IMessageSerializer>();
        }


        /// <inheritdoc />
        public async Task<ConsumeResult> Consume(string exchange, string routingKey, IMessageProperties properties, byte[] body)
        {
            object? message = null;
            try
            {
                try
                {
                    message = messageSerializer.Deserialize(body, properties);
                    if (message == null)
                        throw new ArgumentException($"Message body for routing key '{routingKey}' could not be deserialized into a message object", nameof(body));
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Message body for routing key '{routingKey}' could not be deserialized into a message object: {e.Message}", nameof(body), e);
                }

                return await DispatchMessage(message, new MessageContextData
                {
                    RawBody = body,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Properties = properties
                }).ConfigureAwait(false);
            }
            catch (Exception dispatchException)
            {
                await using var emptyContext = new MessageContext
                {
                    Config = config,
                    Queue = queueName,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    RawBody = body,
                    Message = message,
                    Properties = properties,
                    Binding = new ExceptionContextBinding(queueName),
                    ConnectionClosed = CancellationToken.None
                };
                
                var exceptionContext = new ExceptionStrategyContext(emptyContext, dispatchException);
                await HandleException(exceptionContext).ConfigureAwait(false);
                
                return exceptionContext.ConsumeResult;
            }
        }


        private async Task<ConsumeResult> DispatchMessage(object message, MessageContextData messageContextData)
        {
            var returnResult = ConsumeResult.Success;
            var messageType = message.GetType();
            var validMessageType = false;

            foreach (var binding in bindings.Where(binding => binding.Accept(messageType)))
            {
                var consumeResult = await InvokeUsingBinding(message, messageContextData, binding).ConfigureAwait(false);
                validMessageType = true;

                if (consumeResult != ConsumeResult.Success)
                    returnResult = consumeResult;
            }

            if (!validMessageType)
                throw new ArgumentException($"No binding found for message type: {message.GetType().FullName}");

            return returnResult;
        }


        private async Task<ConsumeResult> InvokeUsingBinding(object message, MessageContextData messageContextData, IBinding binding)
        {
            await using var context = new MessageContext
            {
                Config = config,
                Queue = queueName,
                Exchange = messageContextData.Exchange,
                RoutingKey = messageContextData.RoutingKey,
                RawBody = messageContextData.RawBody,
                Message = message,
                Properties = messageContextData.Properties,
                Binding = binding,
                ConnectionClosed = cancellationToken
            };
            
            try
            {
                await MiddlewareHelper.GoAsync(config.Middleware.Message,
                    async (handler, next) => await handler.Handle(context, next).ConfigureAwait(false),
                    async () => { await binding.Invoke(context).ConfigureAwait(false); });

                await binding.Cleanup(context, ConsumeResult.Success).ConfigureAwait(false);
                return ConsumeResult.Success;
            }
            catch (Exception invokeException)
            {
                var exceptionContext = new ExceptionStrategyContext(context, invokeException);
                await HandleException(exceptionContext).ConfigureAwait(false);

                await binding.Cleanup(context, exceptionContext.ConsumeResult).ConfigureAwait(false);
                return exceptionContext.ConsumeResult;
            }
        }


        private async Task HandleException(ExceptionStrategyContext exceptionContext)
        {
            if (cancellationToken.IsCancellationRequested && IgnoreExceptionDuringShutdown(exceptionContext.Exception))
            {
                // The service is most likely stopping, and the connection is gone anyways.
                exceptionContext.SetConsumeResult(ConsumeResult.Requeue);
                return;
            }

            try
            {
                await exceptionStrategy.HandleException(exceptionContext).ConfigureAwait(false);
            }
            catch (Exception strategyException)
            {
                // Exception in the exception strategy. Oh dear.
                exceptionContext.SetConsumeResult(ConsumeResult.Error);
                logger.ConsumeException(strategyException, exceptionContext.MessageContext, ConsumeResult.Error);
            }

            logger.ConsumeException(exceptionContext.Exception, exceptionContext.MessageContext, exceptionContext.ConsumeResult);
        }


        private static bool IgnoreExceptionDuringShutdown(Exception e)
        {
            return e switch
            {
                AggregateException aggregateException => aggregateException.InnerExceptions.Any(IgnoreExceptionDuringShutdown),
                OperationCanceledException => true,
                _ => e.InnerException != null && IgnoreExceptionDuringShutdown(e.InnerException)
            };
        }


        private struct MessageContextData
        {
            public byte[] RawBody;
            public string Exchange;
            public string RoutingKey;
            public IMessageProperties Properties;
        }


        private class ExceptionContextBinding : IBinding
        {
            public string? QueueName { get; }
            public QueueType? QueueType => null;


            public ExceptionContextBinding(string? queueName)
            {
                QueueName = queueName;
            }


            public ValueTask Apply(IBindingTarget target)
            {
                throw new InvalidOperationException("Apply method should not be called on a binding in an Exception context");
            }


            public bool Accept(Type messageClass)
            {
                throw new InvalidOperationException("Accept method should not be called on a binding in an Exception context");
            }


            public ValueTask Invoke(IMessageContext context)
            {
                throw new InvalidOperationException("Invoke method should not be called on a binding in an Exception context");
            }


            public ValueTask Cleanup(IMessageContext context, ConsumeResult consumeResult)
            {
                throw new InvalidOperationException("Cleanup method should not be called on a binding in an Exception context");
            }
        }
    }
}

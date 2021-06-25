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
    /// <inheritdoc />
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
        public async Task<ConsumeResult> Consume(string exchange, string routingKey, IMessageProperties properties, ReadOnlyMemory<byte> body)
        {
            object message = null;
            try
            {
                message = messageSerializer.Deserialize(body.ToArray(), properties);
                if (message == null)
                    throw new ArgumentException("Message body could not be deserialized into a message object", nameof(body));

                return await DispatchMessage(message, new MessageContextData
                {
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Properties = properties
                });
            }
            catch (Exception dispatchException)
            {
                using (var emptyContext = new MessageContext
                {
                    Config = config,
                    Queue = queueName,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Message = message,
                    Properties = properties,
                    Binding = null
                })
                {
                    var exceptionContext = new ExceptionStrategyContext(emptyContext, dispatchException);
                    HandleException(exceptionContext);
                    return exceptionContext.ConsumeResult;
                }
            }
        }


        private async Task<ConsumeResult> DispatchMessage(object message, MessageContextData messageContextData)
        {
            var returnResult = ConsumeResult.Success;
            var messageType = message.GetType();
            var validMessageType = false;

            foreach (var binding in bindings.Where(binding => binding.Accept(messageType)))
            {
                var consumeResult = await InvokeUsingBinding(message, messageContextData, binding);
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
            using (var context = new MessageContext
            {
                Config = config,
                Queue = queueName,
                Exchange = messageContextData.Exchange,
                RoutingKey = messageContextData.RoutingKey,
                Message = message,
                Properties = messageContextData.Properties,
                Binding = binding
            })
            {
                try
                {
                    await MiddlewareHelper.GoAsync(config.Middleware.Message,
                        async (handler, next) => await handler.Handle(context, next),
                        async () => { await binding.Invoke(context); });

                    await binding.Cleanup(context, ConsumeResult.Success);
                    return ConsumeResult.Success;
                }
                catch (Exception invokeException)
                {
                    var exceptionContext = new ExceptionStrategyContext(context, invokeException);
                    HandleException(exceptionContext);

                    await binding.Cleanup(context, exceptionContext.ConsumeResult);
                    return exceptionContext.ConsumeResult;
                }
            }
        }


        private void HandleException(ExceptionStrategyContext exceptionContext)
        {
            if (cancellationToken.IsCancellationRequested && IgnoreExceptionDuringShutdown(exceptionContext.Exception))
            {
                // The service is most likely stopping, and the connection is gone anyways.
                exceptionContext.SetConsumeResult(ConsumeResult.Requeue);
                return;
            }

            try
            {
                exceptionStrategy.HandleException(exceptionContext);
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
            switch (e)
            {
                case AggregateException aggregateException:
                    return aggregateException.InnerExceptions.Any(IgnoreExceptionDuringShutdown);

                case TaskCanceledException _:
                case OperationCanceledException _:  // thrown by CancellationTokenSource.ThrowIfCancellationRequested
                    return true;

                default:
                    return e.InnerException != null && IgnoreExceptionDuringShutdown(e.InnerException);
            }
        }


        private struct MessageContextData
        {
            public string Exchange;
            public string RoutingKey;
            public IMessageProperties Properties;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
    public class TapetiConsumer : IConsumer
    {
        private readonly ITapetiConfig config;
        private readonly string queueName;
        private readonly List<IBinding> bindings;

        private readonly ILogger logger;
        private readonly IExceptionStrategy exceptionStrategy;
        private readonly IMessageSerializer messageSerializer;


        /// <inheritdoc />
        public TapetiConsumer(ITapetiConfig config, string queueName, IEnumerable<IBinding> bindings)
        {
            this.config = config;
            this.queueName = queueName;
            this.bindings = bindings.ToList();

            logger = config.DependencyResolver.Resolve<ILogger>();
            exceptionStrategy = config.DependencyResolver.Resolve<IExceptionStrategy>();
            messageSerializer = config.DependencyResolver.Resolve<IMessageSerializer>();
        }


        /// <inheritdoc />
        public async Task<ConsumeResponse> Consume(string exchange, string routingKey, IMessageProperties properties, byte[] body)
        {
            try
            {
                var message = messageSerializer.Deserialize(body, properties);
                if (message == null)
                    throw new ArgumentException($"Message body could not be deserialized into a message object in queue {queueName}", nameof(body));

                await DispatchMessage(message, new MessageContextData
                {
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Properties = properties
                });

                return ConsumeResponse.Ack;
            }
            catch (Exception e)
            {
                // TODO exception strategy
                // TODO logger
                return ConsumeResponse.Nack;
            }


            /*

                    handlingResult = new HandlingResult
                    {
                        ConsumeResponse = ConsumeResponse.Ack,
                        MessageAction = MessageAction.None
                    };
                }
                catch (Exception eDispatch)
                {
                    var exception = ExceptionDispatchInfo.Capture(UnwrapException(eDispatch));
                    logger.HandlerException(eDispatch);
                    try
                    {
                        var exceptionStrategyContext = new ExceptionStrategyContext(context, exception.SourceException);

                        exceptionStrategy.HandleException(exceptionStrategyContext);

                        handlingResult = exceptionStrategyContext.HandlingResult.ToHandlingResult();
                    }
                    catch (Exception eStrategy)
                    {
                        logger.HandlerException(eStrategy);
                    }
                }
                try
                {
                    if (handlingResult == null)
                    {
                        handlingResult = new HandlingResult
                        {
                            ConsumeResponse = ConsumeResponse.Nack,
                            MessageAction = MessageAction.None
                        };
                    }
                    await RunCleanup(context, handlingResult);
                }
                catch (Exception eCleanup)
                {
                    logger.HandlerException(eCleanup);
                }
            }
            finally
            {
                try
                {
                    if (handlingResult == null)
                    {
                        handlingResult = new HandlingResult
                        {
                            ConsumeResponse = ConsumeResponse.Nack,
                            MessageAction = MessageAction.None
                        };
                    }
                    await client.Respond(deliveryTag, handlingResult.ConsumeResponse);
                }
                catch (Exception eRespond)
                {
                    logger.HandlerException(eRespond);
                }
                try
                {
                    context?.Dispose();
                }
                catch (Exception eDispose)
                {
                    logger.HandlerException(eDispose);
                }
            }
            */
        }


        private async Task DispatchMessage(object message, MessageContextData messageContextData)
        {
            var messageType = message.GetType();
            var validMessageType = false;

            foreach (var binding in bindings)
            {
                if (!binding.Accept(messageType)) 
                    continue;

                await InvokeUsingBinding(message, messageContextData, binding);
                validMessageType = true;
            }

            if (!validMessageType)
                throw new ArgumentException($"Unsupported message type in queue {queueName}: {message.GetType().FullName}");
        }


        private async Task InvokeUsingBinding(object message, MessageContextData messageContextData, IBinding binding)
        {
            var context = new MessageContext
            {
                Config = config,
                Queue = queueName,
                Exchange = messageContextData.Exchange,
                RoutingKey = messageContextData.RoutingKey,
                Message = message,
                Properties = messageContextData.Properties,
                Binding = binding
            };

            try
            {
                await MiddlewareHelper.GoAsync(config.Middleware.Message,
                    (handler, next) => handler.Handle(context, next),
                    async () => { await binding.Invoke(context); });
            }
            finally
            {
                context.Dispose();
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

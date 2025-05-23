using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Config.Annotations;
using Tapeti.Default;
using Tapeti.Helpers;

namespace Tapeti.Connection
{
    /// <inheritdoc />
    internal class TapetiPublisher : IInternalPublisher
    {
        private readonly ITapetiConfig config;
        private readonly Func<ITapetiChannel> channelFactory;
        private readonly IExchangeStrategy exchangeStrategy;
        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly IMessageSerializer messageSerializer;


        public TapetiPublisher(ITapetiConfig config, Func<ITapetiChannel> channelFactory)
        {
            this.config = config;
            this.channelFactory = channelFactory;

            exchangeStrategy = config.DependencyResolver.Resolve<IExchangeStrategy>();
            routingKeyStrategy = config.DependencyResolver.Resolve<IRoutingKeyStrategy>();
            messageSerializer = config.DependencyResolver.Resolve<IMessageSerializer>();
        }


        /// <inheritdoc />
        public async Task Publish(object message)
        {
            await Publish(message, null, IsMandatory(message)).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task PublishRequest<TController, TRequest, TResponse>(TRequest message, Expression<Func<TController, Action<TResponse>>> responseMethodSelector) where TController : class where TRequest : class where TResponse : class
        {
            await PublishRequest(message, responseMethodSelector.Body).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task PublishRequest<TController, TRequest, TResponse>(TRequest message, Expression<Func<TController, Func<TResponse, Task>>> responseMethodSelector) where TController : class where TRequest : class where TResponse : class
        {
            await PublishRequest(message, responseMethodSelector.Body).ConfigureAwait(false);
        }


        private async Task PublishRequest(object message, Expression responseMethodBody)
        {
            var callExpression = (responseMethodBody as UnaryExpression)?.Operand as MethodCallExpression;
            var targetMethodExpression = callExpression?.Object as ConstantExpression;

            var responseHandler = targetMethodExpression?.Value as MethodInfo;
            if (responseHandler == null)
                throw new ArgumentException("Unable to determine the response method", nameof(responseMethodBody));


            var requestAttribute = message.GetType().GetCustomAttribute<RequestAttribute>();
            if (requestAttribute?.Response == null)
                throw new ArgumentException($"Request message {message.GetType().Name} must be marked with the Request attribute and a valid Response type", nameof(message));

            var binding = config.Bindings.ForMethod(responseHandler);
            if (binding == null)
                throw new ArgumentException("responseHandler must be a registered message handler", nameof(responseHandler));

            if (!binding.Accept(requestAttribute.Response))
                throw new ArgumentException($"responseHandler must accept message of type {requestAttribute.Response}", nameof(responseHandler));

            var responseHandleAttribute = binding.Method.GetResponseHandlerAttribute();
            if (responseHandleAttribute == null)
                throw new ArgumentException("responseHandler must be marked with the ResponseHandler attribute", nameof(responseHandler));

            if (binding.QueueName == null)
                throw new ArgumentException("responseHandler is not yet subscribed to a queue, TapetiConnection.Subscribe must be called before starting a request", nameof(responseHandler));


            var properties = new MessageProperties
            {
                CorrelationId = ResponseFilterMiddleware.CorrelationIdRequestPrefix + MethodSerializer.Serialize(responseHandler),
                ReplyTo = binding.QueueName
            };

            await Publish(message, properties, IsMandatory(message)).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task SendToQueue(string queueName, object message)
        {
            await PublishDirect(message, queueName, null, IsMandatory(message)).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task Publish(object message, IMessageProperties? properties, bool mandatory)
        {
            var messageClass = message.GetType();
            var exchange = exchangeStrategy.GetExchange(messageClass);
            var routingKey = routingKeyStrategy.GetRoutingKey(messageClass);

            await Publish(message, properties, exchange, routingKey, mandatory).ConfigureAwait(false);
        }


        /// <inheritdoc />
        public async Task PublishDirect(object message, string queueName, IMessageProperties? properties, bool mandatory)
        {
            await Publish(message, properties, null, queueName, mandatory).ConfigureAwait(false);
        }


        private async Task Publish(object message, IMessageProperties? properties, string? exchange, string routingKey, bool mandatory)
        {
            var writableProperties = new MessageProperties(properties);

            writableProperties.Timestamp ??= DateTime.UtcNow;
            writableProperties.Persistent = true;


            var context = new PublishContext
            {
                Config = config,
                Exchange = exchange,
                RoutingKey = routingKey,
                Message = message,
                Properties = writableProperties
            };


            await MiddlewareHelper.GoAsync(
                config.Middleware.Publish,
                async (handler, next) => await handler.Handle(context, next).ConfigureAwait(false),
                async () =>
                {
                    var body = messageSerializer.Serialize(message, writableProperties);
                    await channelFactory().Enqueue(transportChannel => transportChannel.Publish(body, writableProperties, exchange, routingKey, mandatory)).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }


        private static bool IsMandatory(object message)
        {
            return message.GetType().GetCustomAttribute<MandatoryAttribute>() != null;
        }


        private class PublishContext : IPublishContext
        {
            public ITapetiConfig Config { get; init; } = null!;
            public string? Exchange { get; set; }
            public string RoutingKey { get; init; } = null!;
            public object Message { get; init; } = null!;
            public IMessageProperties? Properties { get; init; }
        }
    }
}

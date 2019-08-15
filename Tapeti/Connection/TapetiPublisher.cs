using System;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Config;
using Tapeti.Default;
using Tapeti.Helpers;

namespace Tapeti.Connection
{
    /// <inheritdoc />
    internal class TapetiPublisher : IInternalPublisher
    {
        private readonly ITapetiConfig config;
        private readonly Func<ITapetiClient> clientFactory;
        private readonly IExchangeStrategy exchangeStrategy;
        private readonly IRoutingKeyStrategy routingKeyStrategy;
        private readonly IMessageSerializer messageSerializer;


        /// <inheritdoc />
        public TapetiPublisher(ITapetiConfig config, Func<ITapetiClient> clientFactory)
        {
            this.config = config;
            this.clientFactory = clientFactory;

            exchangeStrategy = config.DependencyResolver.Resolve<IExchangeStrategy>();
            routingKeyStrategy = config.DependencyResolver.Resolve<IRoutingKeyStrategy>();
            messageSerializer = config.DependencyResolver.Resolve<IMessageSerializer>();
        }


        /// <inheritdoc />
        public async Task Publish(object message)
        {
            await Publish(message, null, IsMandatory(message));
        }


        /// <inheritdoc />
        public async Task Publish(object message, IMessageProperties properties, bool mandatory)
        {
            var messageClass = message.GetType();
            var exchange = exchangeStrategy.GetExchange(messageClass);
            var routingKey = routingKeyStrategy.GetRoutingKey(messageClass);

            await Publish(message, properties, exchange, routingKey, mandatory);
        }


        /// <inheritdoc />
        public async Task PublishDirect(object message, string queueName, IMessageProperties properties, bool mandatory)
        {
            await Publish(message, properties, null, queueName, mandatory);
        }


        private async Task Publish(object message, IMessageProperties properties, string exchange, string routingKey, bool mandatory)
        {
            var writableProperties = new MessageProperties(properties);

            if (!writableProperties.Timestamp.HasValue)
                writableProperties.Timestamp = DateTime.UtcNow;

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
                async (handler, next) => await handler.Handle(context, next),
                async () =>
                {
                    var body = messageSerializer.Serialize(message, writableProperties);
                    await clientFactory().Publish(body, writableProperties, exchange, routingKey, mandatory);
                });
        }


        private static bool IsMandatory(object message)
        {
            return message.GetType().GetCustomAttribute<MandatoryAttribute>() != null;
        }


        private class PublishContext : IPublishContext
        {
            public ITapetiConfig Config { get; set; }
            public string Exchange { get; set; }
            public string RoutingKey { get; set; }
            public object Message { get; set; }
            public IMessageProperties Properties { get; set; }
        }
    }
}

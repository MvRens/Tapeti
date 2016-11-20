using System;
using RabbitMQ.Client;

namespace Tapeti.Registration
{
    public class ControllerDynamicQueueRegistration : AbstractControllerRegistration
    {
        private readonly Func<IRoutingKeyStrategy> routingKeyStrategyFactory;


        public ControllerDynamicQueueRegistration(Func<IControllerFactory> controllerFactoryFactory, Func<IRoutingKeyStrategy> routingKeyStrategyFactory, Type controllerType, string defaultExchange) 
            : base(controllerFactoryFactory, controllerType, defaultExchange)
        {
            this.routingKeyStrategyFactory = routingKeyStrategyFactory;
        }


        public override string BindQueue(IModel channel)
        {
            var queue = channel.QueueDeclare();

            foreach (var messageType in GetMessageTypes())
            {
                var routingKey = routingKeyStrategyFactory().GetRoutingKey(messageType);

                foreach (var exchange in GetMessageExchanges(messageType))
                    channel.QueueBind(queue.QueueName, exchange, routingKey);
            }

            return queue.QueueName;
        }
    }
}

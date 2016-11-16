using System;
using RabbitMQ.Client;

namespace Tapeti.Registration
{
    public class ControllerDynamicQueueRegistration : AbstractControllerRegistration
    {
        private readonly IRoutingKeyStrategy routingKeyStrategy;


        public ControllerDynamicQueueRegistration(IControllerFactory controllerFactory, IRoutingKeyStrategy routingKeyStrategy, Type controllerType) 
            : base(controllerFactory, controllerType)
        {
            this.routingKeyStrategy = routingKeyStrategy;
        }


        public override void ApplyTopology(IModel channel)
        {
            var queue = channel.QueueDeclare();

            foreach (var messageType in GetMessageTypes())
            {
                //TODO use routing key attribute(s) for method or use strategy
                //TODO use exchange attribute or default setting

                //channel.QueueBind(queue.QueueName, );
            }
        }
    }
}

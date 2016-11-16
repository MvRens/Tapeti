using System;
using RabbitMQ.Client;

namespace Tapeti.Registration
{
    public class ControllerQueueRegistration : AbstractControllerRegistration
    {
        private readonly string queueName;

        public ControllerQueueRegistration(IControllerFactory controllerFactory, IRoutingKeyStrategy routingKeyStrategy, Type controllerType, string queueName) : base(controllerFactory, controllerType)
        {
            this.queueName = queueName;
        }


        public override void ApplyTopology(IModel channel)
        {
            channel.QueueDeclarePassive(queueName);
        }
    }
}

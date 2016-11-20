using System;
using RabbitMQ.Client;

namespace Tapeti.Registration
{
    public class ControllerQueueRegistration : AbstractControllerRegistration
    {
        private readonly string queueName;

        public ControllerQueueRegistration(Func<IControllerFactory> controllerFactoryFactory, Type controllerType, string defaultExchange, string queueName) : base(controllerFactoryFactory, controllerType, defaultExchange)
        {
            this.queueName = queueName;
        }


        public override string BindQueue(IModel channel)
        {
            return channel.QueueDeclarePassive(queueName).QueueName;
        }
    }
}

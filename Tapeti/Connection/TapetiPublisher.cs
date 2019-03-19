﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Annotations;

namespace Tapeti.Connection
{
    public class TapetiPublisher : IInternalPublisher
    {
        private readonly Func<TapetiWorker> workerFactory;


        public TapetiPublisher(Func<TapetiWorker> workerFactory)
        {
            this.workerFactory = workerFactory;
        }


        public Task Publish(object message)
        {
            return workerFactory().Publish(message, null, IsMandatory(message));
        }


        public Task Publish(object message, IBasicProperties properties, bool mandatory)
        {
            return workerFactory().Publish(message, properties, mandatory);
        }


        public Task PublishDirect(object message, string queueName, IBasicProperties properties, bool mandatory)
        {
            return workerFactory().PublishDirect(message, queueName, properties, mandatory);
        }


        private static bool IsMandatory(object message)
        {
            return message.GetType().GetCustomAttribute<MandatoryAttribute>() != null;
        }
    }
}

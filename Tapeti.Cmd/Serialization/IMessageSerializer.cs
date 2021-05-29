﻿using System;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace Tapeti.Cmd.Serialization
{
    public class Message
    {
        public ulong DeliveryTag;
        public bool Redelivered;
        public string Exchange;
        public string RoutingKey;
        public string Queue;
        public IBasicProperties Properties;
        public byte[] Body;
    }


    public interface IMessageSerializer : IDisposable
    {
        void Serialize(Message message);
        IEnumerable<Message> Deserialize(IModel channel);
    }
}

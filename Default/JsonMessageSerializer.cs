using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RabbitMQ.Client;

namespace Tapeti.Default
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        protected const string ContentType = "application/json";
        protected const string ClassTypeHeader = "classType";


        private readonly ConcurrentDictionary<string, Type> deserializedTypeNames = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<Type, string> serializedTypeNames = new ConcurrentDictionary<Type, string>();
        private readonly JsonSerializerSettings serializerSettings;

        public JsonMessageSerializer()
        {
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            serializerSettings.Converters.Add(new StringEnumConverter());
        }


        public byte[] Serialize(object message, IBasicProperties properties)
        {
            if (properties.Headers == null)
                properties.Headers = new Dictionary<string, object>();

            var typeName = serializedTypeNames.GetOrAdd(message.GetType(), SerializeTypeName);

            properties.Headers.Add(ClassTypeHeader, Encoding.UTF8.GetBytes(typeName));
            properties.ContentType = ContentType;

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message, serializerSettings));
        }


        public object Deserialize(byte[] body, IBasicProperties properties)
        {
            object typeName;

            if (properties.ContentType == null || !properties.ContentType.Equals(ContentType))
                throw new ArgumentException($"content_type must be {ContentType}");

            if (properties.Headers == null || !properties.Headers.TryGetValue(ClassTypeHeader, out typeName))
                throw new ArgumentException($"{ClassTypeHeader} header not present");

            var messageType = deserializedTypeNames.GetOrAdd(Encoding.UTF8.GetString((byte[])typeName), DeserializeTypeName);
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body), messageType);
        }



        public virtual Type DeserializeTypeName(string typeName)
        {
            var parts = typeName.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid type name {typeName}");

            var type = Type.GetType(parts[0] + "," + parts[1]);
            if (type == null)
                throw new ArgumentException($"Unable to resolve type {typeName}");

            return type;
        }

        public virtual string SerializeTypeName(Type type)
        {
            var typeName = type.FullName + ":" + type.Assembly.GetName().Name;
            if (typeName.Length > 255)
                throw new ArgumentException($"Type name {typeName} exceeds AMQP 255 character limit");

            return typeName;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// IMessageSerializer implementation for JSON encoding and decoding using Newtonsoft.Json.
    /// </summary>
    public class JsonMessageSerializer : IMessageSerializer
    {
        private const string ContentType = "application/json";
        private const string ClassTypeHeader = "classType";


        private readonly ConcurrentDictionary<string, Type> deserializedTypeNames = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<Type, string> serializedTypeNames = new ConcurrentDictionary<Type, string>();
        private readonly JsonSerializerSettings serializerSettings;


        /// <summary>
        /// </summary>
        public JsonMessageSerializer()
        {
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            serializerSettings.Converters.Add(new FallbackStringEnumConverter());            
        }


        /// <inheritdoc />
        public byte[] Serialize(object message, IMessageProperties properties)
        {
            var typeName = serializedTypeNames.GetOrAdd(message.GetType(), SerializeTypeName);

            properties.SetHeader(ClassTypeHeader, typeName);
            properties.ContentType = ContentType;

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message, serializerSettings));
        }


        /// <inheritdoc />
        public object Deserialize(byte[] body, IMessageProperties properties)
        {
            if (!(properties.ContentType is ContentType))
                throw new ArgumentException($"content_type must be {ContentType}");

            var typeName = properties.GetHeader(ClassTypeHeader);
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException($"{ClassTypeHeader} header not present");

            var messageType = deserializedTypeNames.GetOrAdd(typeName, DeserializeTypeName);
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(body), messageType, serializerSettings);
        }



        /// <summary>
        /// Resolves a Type based on the serialized type name.
        /// </summary>
        /// <param name="typeName">The type name in the format FullNamespace.ClassName:AssemblyName</param>
        /// <returns>The resolved Type</returns>
        /// <exception cref="ArgumentException">If the format is unrecognized or the Type could not be resolved</exception>
        protected virtual Type DeserializeTypeName(string typeName)
        {
            var parts = typeName.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid type name {typeName}");

            var type = Type.GetType(parts[0] + "," + parts[1]);
            if (type == null)
                throw new ArgumentException($"Unable to resolve type {typeName}");

            return type;
        }


        /// <summary>
        /// Serializes a Type into a string representation.
        /// </summary>
        /// <param name="type">The type to serialize</param>
        /// <returns>The type name in the format FullNamespace.ClassName:AssemblyName</returns>
        /// <exception cref="ArgumentException">If the serialized type name results in the AMQP limit of 255 characters to be exceeded</exception>
        protected virtual string SerializeTypeName(Type type)
        {
            var typeName = type.FullName + ":" + type.Assembly.GetName().Name;
            if (typeName.Length > 255)
                throw new ArgumentException($"Type name {typeName} exceeds AMQP 255 character limit");

            return typeName;
        }
    }
}

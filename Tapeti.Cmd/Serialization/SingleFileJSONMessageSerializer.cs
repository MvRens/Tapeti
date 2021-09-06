using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;

namespace Tapeti.Cmd.Serialization
{
    public class SingleFileJSONMessageSerializer : IMessageSerializer
    {
        private readonly Stream stream;
        private readonly bool disposeStream;
        private readonly Encoding encoding;

        // StreamReader.DefaultBufferSize is private :-/
        private const int DefaultBufferSize = 1024;


        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore            
        };

        private readonly Lazy<StreamWriter> exportFile;


        public SingleFileJSONMessageSerializer(Stream stream, bool disposeStream, Encoding encoding)
        {
            this.stream = stream;
            this.disposeStream = disposeStream;
            this.encoding = encoding;

            exportFile = new Lazy<StreamWriter>(() => new StreamWriter(stream, encoding));
        }


        public void Serialize(Message message)
        {
            var serializableMessage = new SerializableMessage(message);
            var serialized = JsonConvert.SerializeObject(serializableMessage, SerializerSettings);
            exportFile.Value.WriteLine(serialized);
        }

        
        public int GetMessageCount()
        {
            if (!stream.CanSeek)
                return 0;

            var position = stream.Position;
            try
            {
                var lineCount = 0;
                using var reader = new StreamReader(stream, encoding, true, DefaultBufferSize, true);

                while (!reader.EndOfStream)
                {
                    if (!string.IsNullOrEmpty(reader.ReadLine()))
                        lineCount++;
                }

                return lineCount;
            }
            finally
            {
                stream.Position = position;
            }
        }


        public IEnumerable<Message> Deserialize(IModel channel)
        {
            using var reader = new StreamReader(stream, encoding, true, DefaultBufferSize, true);
            
            while (!reader.EndOfStream)
            {
                var serialized = reader.ReadLine();
                if (string.IsNullOrEmpty(serialized))
                    continue;

                var serializableMessage = JsonConvert.DeserializeObject<SerializableMessage>(serialized);
                if (serializableMessage == null)
                    continue;

                yield return serializableMessage.ToMessage(channel);
            }
        }


        public void Dispose()
        {
            if (exportFile.IsValueCreated)
                exportFile.Value.Dispose();

            if (disposeStream)
                stream.Dispose();
        }



        // ReSharper disable MemberCanBePrivate.Local - used for JSON serialization
        // ReSharper disable NotAccessedField.Local
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        private class SerializableMessage
        {          
            public ulong DeliveryTag;
            public bool Redelivered;
            public string Exchange;
            public string RoutingKey;
            public string Queue;

            // ReSharper disable once FieldCanBeMadeReadOnly.Local - must be settable by JSON deserialization
            public SerializableMessageProperties Properties;

            public JObject Body;
            public byte[] RawBody;


            // ReSharper disable once UnusedMember.Global - used by JSON deserialization
            // ReSharper disable once UnusedMember.Local
            public SerializableMessage()
            {
                Properties = new SerializableMessageProperties();
            }


            public SerializableMessage(Message fromMessage)
            {
                DeliveryTag = fromMessage.DeliveryTag;
                Redelivered = fromMessage.Redelivered;
                Exchange = fromMessage.Exchange;
                RoutingKey = fromMessage.RoutingKey;
                Queue = fromMessage.Queue;
                Properties = new SerializableMessageProperties(fromMessage.Properties);

                // If this is detected as a JSON message, include the object directly in the JSON line so that it is easier
                // to read and process in the output file. Otherwise simply include the raw data and let Newtonsoft encode it.
                // This does mean the message will be rewritten. If this is an issue, feel free to add a "raw" option to this tool
                // that forces the RawBody to be used. It is open-source after all :-).
                if (Properties.ContentType == "application/json")
                {
                    try
                    {
                        Body = JObject.Parse(Encoding.UTF8.GetString(fromMessage.Body));
                        RawBody = null;
                    }
                    catch
                    {
                        // Fall back to using the raw body
                        Body = null;
                        RawBody = fromMessage.Body;
                    }
                }
                else
                {
                    Body = null;
                    RawBody = fromMessage.Body;
                }
            }


            public Message ToMessage(IModel channel)
            {
                return new()
                {
                    DeliveryTag = DeliveryTag,
                    Redelivered = Redelivered,
                    Exchange = Exchange,
                    RoutingKey = RoutingKey,
                    Queue = Queue,
                    Properties = Properties.ToBasicProperties(channel),
                    Body = Body != null
                        ? Encoding.UTF8.GetBytes(Body.ToString(Formatting.None))
                        : RawBody
                };
            }
        }


        // IBasicProperties is finicky when it comes to writing it's properties,
        // so we need this normalized class to read and write it from and to JSON
        private class SerializableMessageProperties
        {
            public string AppId;
            public string ClusterId;
            public string ContentEncoding;
            public string ContentType;
            public string CorrelationId;
            public byte? DeliveryMode;
            public string Expiration;
            public IDictionary<string, string> Headers;
            public string MessageId;
            public byte? Priority;
            public string ReplyTo;
            public long? Timestamp;
            public string Type;
            public string UserId;


            public SerializableMessageProperties()
            {            
            }


            public SerializableMessageProperties(IBasicProperties fromProperties)
            {
                AppId = fromProperties.AppId;
                ClusterId = fromProperties.ClusterId;
                ContentEncoding = fromProperties.ContentEncoding;
                ContentType = fromProperties.ContentType;
                CorrelationId = fromProperties.CorrelationId;
                DeliveryMode = fromProperties.IsDeliveryModePresent() ? (byte?)fromProperties.DeliveryMode : null;
                Expiration = fromProperties.Expiration;
                MessageId = fromProperties.MessageId;
                Priority = fromProperties.IsPriorityPresent() ? (byte?) fromProperties.Priority : null;
                ReplyTo = fromProperties.ReplyTo;                
                Timestamp = fromProperties.IsTimestampPresent() ? (long?)fromProperties.Timestamp.UnixTime : null;
                Type = fromProperties.Type;
                UserId = fromProperties.UserId;

                if (fromProperties.IsHeadersPresent())
                {
                    Headers = new Dictionary<string, string>();

                    // This assumes header values are UTF-8 encoded strings. This is true for Tapeti.
                    foreach (var (key, value) in fromProperties.Headers)
                        Headers.Add(key, Encoding.UTF8.GetString((byte[])value));
                }
                else
                    Headers = null;
            }


            public IBasicProperties ToBasicProperties(IModel channel)
            {
                var properties = channel.CreateBasicProperties();

                if (!string.IsNullOrEmpty(AppId)) properties.AppId = AppId;
                if (!string.IsNullOrEmpty(ClusterId)) properties.ClusterId = ClusterId;
                if (!string.IsNullOrEmpty(ContentEncoding)) properties.ContentEncoding = ContentEncoding;
                if (!string.IsNullOrEmpty(ContentType)) properties.ContentType = ContentType;
                if (!string.IsNullOrEmpty(CorrelationId)) properties.CorrelationId = CorrelationId;
                if (DeliveryMode.HasValue) properties.DeliveryMode = DeliveryMode.Value;
                if (!string.IsNullOrEmpty(Expiration)) properties.Expiration = Expiration;
                if (!string.IsNullOrEmpty(MessageId)) properties.MessageId = MessageId;
                if (Priority.HasValue) properties.Priority = Priority.Value;
                if (!string.IsNullOrEmpty(ReplyTo)) properties.ReplyTo = ReplyTo;
                if (Timestamp.HasValue) properties.Timestamp = new AmqpTimestamp(Timestamp.Value);
                if (!string.IsNullOrEmpty(Type)) properties.Type = Type;
                if (!string.IsNullOrEmpty(UserId)) properties.UserId = UserId;

                // ReSharper disable once InvertIf
                if (Headers != null)
                {
                    properties.Headers = new Dictionary<string, object>();

                    foreach (var (key, value) in Headers)
                        properties.Headers.Add(key, Encoding.UTF8.GetBytes(value));
                }

                return properties;
            }
        }
        // ReSharper restore FieldCanBeMadeReadOnly.Local
        // ReSharper restore NotAccessedField.Local
        // ReSharper restore MemberCanBePrivate.Local
    }
}

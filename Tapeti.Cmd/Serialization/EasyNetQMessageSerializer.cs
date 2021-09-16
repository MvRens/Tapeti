using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Tapeti.Cmd.Serialization
{
    public class EasyNetQMessageSerializer : IMessageSerializer
    {
        private static readonly Regex InvalidCharRegex = new(@"[\\\/:\*\?\""\<\>|]", RegexOptions.Compiled);

        private readonly Lazy<string> writablePath;
        private int messageCount;

        private readonly Lazy<string[]> files;


        public EasyNetQMessageSerializer(string path)
        {
            writablePath = new Lazy<string>(() =>
            {
                Directory.CreateDirectory(path);
                return path;
            });

            files = new Lazy<string[]>(() => Directory.GetFiles(path, "*.*.message.txt"));
        }


        public static bool OutputExists(string path)
        {
            return Directory.Exists(path) && Directory.GetFiles(path, "*.message.txt").Length > 0;
        }


        public void Dispose()
        {
        }


        public void Serialize(Message message)
        {
            var uniqueFileName = SanitiseQueueName(message.Queue) + "." + messageCount;

            var bodyPath = Path.Combine(writablePath.Value, uniqueFileName + ".message.txt");
            var propertiesPath = Path.Combine(writablePath.Value, uniqueFileName + ".properties.txt");
            var infoPath = Path.Combine(writablePath.Value, uniqueFileName + ".info.txt");

            var properties = new EasyNetQMessageProperties(message.Properties);
            var info = new EasyNetQMessageReceivedInfo(message);

            File.WriteAllText(bodyPath, Encoding.UTF8.GetString(message.Body));
            File.WriteAllText(propertiesPath, JsonConvert.SerializeObject(properties));
            File.WriteAllText(infoPath, JsonConvert.SerializeObject(info));

            messageCount++;
        }


        private static string SanitiseQueueName(string queueName)
        {
            return InvalidCharRegex.Replace(queueName, "_");
        }


        public int GetMessageCount()
        {
            return files.Value.Length;
        }

        
        public IEnumerable<Message> Deserialize(IModel channel)
        {
            foreach (var file in files.Value)
            {
                const string messageTag = ".message.";

                var directoryName = Path.GetDirectoryName(file);
                var fileName = Path.GetFileName(file);
                var propertiesFileName = Path.Combine(directoryName, fileName.Replace(messageTag, ".properties."));
                var infoFileName = Path.Combine(directoryName, fileName.Replace(messageTag, ".info."));

                var body = File.ReadAllText(file);

                var propertiesJson = File.ReadAllText(propertiesFileName);
                var properties = JsonConvert.DeserializeObject<EasyNetQMessageProperties>(propertiesJson);

                var infoJson = File.ReadAllText(infoFileName);
                var info = JsonConvert.DeserializeObject<EasyNetQMessageReceivedInfo>(infoJson);

                if (info == null)
                    continue;

                var message = info.ToMessage();
                if (properties != null)
                    message.Properties = properties.ToBasicProperties(channel);
                
                message.Body = Encoding.UTF8.GetBytes(body);

                yield return message;
            }
        }


        // ReSharper disable MemberCanBePrivate.Local - used by JSON deserialization
        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        private class EasyNetQMessageProperties
        {
            // ReSharper disable once MemberCanBePrivate.Local - used by JSON deserialization
            public EasyNetQMessageProperties()
            {
            }

            public EasyNetQMessageProperties(IBasicProperties basicProperties) : this()
            {
                if (basicProperties.IsContentTypePresent()) ContentType = basicProperties.ContentType;
                if (basicProperties.IsContentEncodingPresent()) ContentEncoding = basicProperties.ContentEncoding;
                if (basicProperties.IsDeliveryModePresent()) DeliveryMode = basicProperties.DeliveryMode;
                if (basicProperties.IsPriorityPresent()) Priority = basicProperties.Priority;
                if (basicProperties.IsCorrelationIdPresent()) CorrelationId = basicProperties.CorrelationId;
                if (basicProperties.IsReplyToPresent()) ReplyTo = basicProperties.ReplyTo;
                if (basicProperties.IsExpirationPresent()) Expiration = basicProperties.Expiration;
                if (basicProperties.IsMessageIdPresent()) MessageId = basicProperties.MessageId;
                if (basicProperties.IsTimestampPresent()) Timestamp = basicProperties.Timestamp.UnixTime;
                if (basicProperties.IsTypePresent()) Type = basicProperties.Type;
                if (basicProperties.IsUserIdPresent()) UserId = basicProperties.UserId;
                if (basicProperties.IsAppIdPresent()) AppId = basicProperties.AppId;
                if (basicProperties.IsClusterIdPresent()) ClusterId = basicProperties.ClusterId;

                if (!basicProperties.IsHeadersPresent()) 
                    return;

                foreach (var (key, value) in basicProperties.Headers)
                    Headers.Add(key, (byte[])value);
            }

            public IBasicProperties ToBasicProperties(IModel channel)
            {
                var basicProperties = channel.CreateBasicProperties();

                if (ContentTypePresent) basicProperties.ContentType = ContentType;
                if (ContentEncodingPresent) basicProperties.ContentEncoding = ContentEncoding;
                if (DeliveryModePresent) basicProperties.DeliveryMode = DeliveryMode;
                if (PriorityPresent) basicProperties.Priority = Priority;
                if (CorrelationIdPresent) basicProperties.CorrelationId = CorrelationId;
                if (ReplyToPresent) basicProperties.ReplyTo = ReplyTo;
                if (ExpirationPresent) basicProperties.Expiration = Expiration;
                if (MessageIdPresent) basicProperties.MessageId = MessageId;
                if (TimestampPresent) basicProperties.Timestamp = new AmqpTimestamp(Timestamp);
                if (TypePresent) basicProperties.Type = Type;
                if (UserIdPresent) basicProperties.UserId = UserId;
                if (AppIdPresent) basicProperties.AppId = AppId;
                if (ClusterIdPresent) basicProperties.ClusterId = ClusterId;

                if (HeadersPresent)
                {
                    basicProperties.Headers = new Dictionary<string, object>(Headers.ToDictionary(p => p.Key, p => (object)p.Value));
                }

                return basicProperties;
            }


            private string contentType;
            public string ContentType
            {
                get => contentType;
                set { contentType = value; ContentTypePresent = true; }
            }

            private string contentEncoding;
            public string ContentEncoding
            {
                get => contentEncoding;
                set { contentEncoding = value; ContentEncodingPresent = true; }
            }

            // The original EasyNetQ.Hosepipe defines this as an IDictionary<string, object>. This causes UTF-8 headers
            // to be serialized as Base64, and deserialized as string, corrupting the republished message.
            // This may cause incompatibilities, but fixes it for dumped Tapeti messages.
            private IDictionary<string, byte[]> headers = new Dictionary<string, byte[]>();
            public IDictionary<string, byte[]> Headers
            {
                get => headers;
                // ReSharper disable once UnusedMember.Local
                set { headers = value; HeadersPresent = true; }
            }

            private byte deliveryMode;
            public byte DeliveryMode
            {
                get => deliveryMode;
                set { deliveryMode = value; DeliveryModePresent = true; }
            }

            private byte priority;
            public byte Priority
            {
                get => priority;
                set { priority = value; PriorityPresent = true; }
            }

            private string correlationId;
            public string CorrelationId
            {
                get => correlationId;
                set { correlationId = value; CorrelationIdPresent = true; }
            }

            private string replyTo;
            public string ReplyTo
            {
                get => replyTo;
                set { replyTo = value; ReplyToPresent = true; }
            }

            private string expiration;
            public string Expiration
            {
                get => expiration;
                set { expiration = value; ExpirationPresent = true; }
            }

            private string messageId;
            public string MessageId
            {
                get => messageId;
                set { messageId = value; MessageIdPresent = true; }
            }

            private long timestamp;
            public long Timestamp
            {
                get => timestamp;
                set { timestamp = value; TimestampPresent = true; }
            }

            private string type;
            public string Type
            {
                get => type;
                set { type = value; TypePresent = true; }
            }

            private string userId;
            public string UserId
            {
                get => userId;
                set { userId = value; UserIdPresent = true; }
            }

            private string appId;
            public string AppId
            {
                get => appId;
                set { appId = value; AppIdPresent = true; }
            }

            private string clusterId;
            public string ClusterId
            {
                get => clusterId;
                set { clusterId = value; ClusterIdPresent = true; }
            }

            public bool ContentTypePresent { get; set; }
            public bool ContentEncodingPresent { get; set; }
            public bool HeadersPresent { get; set; } = true;
            public bool DeliveryModePresent { get; set; }
            public bool PriorityPresent { get; set; }
            public bool CorrelationIdPresent { get; set; }
            public bool ReplyToPresent { get; set; }
            public bool ExpirationPresent { get; set; }
            public bool MessageIdPresent { get; set; }
            public bool TimestampPresent { get; set; }
            public bool TypePresent { get; set; }
            public bool UserIdPresent { get; set; }
            public bool AppIdPresent { get; set; }
            public bool ClusterIdPresent { get; set; }
        }


        private class EasyNetQMessageReceivedInfo
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Local - used by JSON deserialization
            public string ConsumerTag { get; set; }
            public ulong DeliverTag { get; set; }
            public bool Redelivered { get; set; }
            public string Exchange { get; set; }
            public string RoutingKey { get; set; }
            public string Queue { get; set; }


            // ReSharper disable once MemberCanBePrivate.Local - used by JSON deserialization
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedMember.Global
            public EasyNetQMessageReceivedInfo()
            {
            }


            public EasyNetQMessageReceivedInfo(Message fromMessage)
            {
                ConsumerTag = "hosepipe";
                DeliverTag = fromMessage.DeliveryTag;
                Redelivered = fromMessage.Redelivered;
                Exchange = fromMessage.Exchange;
                RoutingKey = fromMessage.RoutingKey;
                Queue = fromMessage.Queue;
            }


            public Message ToMessage()
            {
                return new()
                {
                    //ConsumerTag = 
                    DeliveryTag = DeliverTag,
                    Redelivered = Redelivered,
                    Exchange = Exchange,
                    RoutingKey = RoutingKey,
                    Queue = Queue
                };
            }
        }
        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Local
        // ReSharper restore MemberCanBePrivate.Local
    }
}
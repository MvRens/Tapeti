using System;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace Tapeti.Cmd.Mock
{
    public class MockBasicProperties : IBasicProperties
    {
        public ushort ProtocolClassId { get; set; }
        public string ProtocolClassName { get; set; }
        
        public void ClearAppId()
        {
            throw new NotImplementedException();
        }

        public void ClearClusterId()
        {
            throw new NotImplementedException();
        }

        public void ClearContentEncoding()
        {
            throw new NotImplementedException();
        }

        public void ClearContentType()
        {
            throw new NotImplementedException();
        }

        public void ClearCorrelationId()
        {
            throw new NotImplementedException();
        }

        public void ClearDeliveryMode()
        {
            throw new NotImplementedException();
        }

        public void ClearExpiration()
        {
            throw new NotImplementedException();
        }

        public void ClearHeaders()
        {
            throw new NotImplementedException();
        }

        public void ClearMessageId()
        {
            throw new NotImplementedException();
        }

        public void ClearPriority()
        {
            throw new NotImplementedException();
        }

        public void ClearReplyTo()
        {
            throw new NotImplementedException();
        }

        public void ClearTimestamp()
        {
            throw new NotImplementedException();
        }

        public void ClearType()
        {
            throw new NotImplementedException();
        }

        public void ClearUserId()
        {
            throw new NotImplementedException();
        }

        public bool IsAppIdPresent() => appIdPresent;
        public bool IsClusterIdPresent() => clusterIdPresent;
        public bool IsContentEncodingPresent() => contentEncodingPresent;
        public bool IsContentTypePresent() => contentTypePresent;
        public bool IsCorrelationIdPresent() => correlationIdPresent;
        public bool IsDeliveryModePresent() => deliveryModePresent;
        public bool IsExpirationPresent() => expirationPresent;
        public bool IsHeadersPresent() => headersPresent;
        public bool IsMessageIdPresent() => messageIdPresent;
        public bool IsPriorityPresent() => priorityPresent;
        public bool IsReplyToPresent() => replyToPresent;
        public bool IsTimestampPresent() => timestampPresent;
        public bool IsTypePresent() => typePresent;
        public bool IsUserIdPresent() => userIdPresent;
        

        private bool appIdPresent;
        private string appId;

        private bool clusterIdPresent;
        private string clusterId;

        private bool contentEncodingPresent;
        private string contentEncoding;

        private bool contentTypePresent;
        private string contentType;

        private bool correlationIdPresent;
        private string correlationId;

        private bool deliveryModePresent;
        private byte deliveryMode;

        private bool expirationPresent;
        private string expiration;

        private bool headersPresent;
        private IDictionary<string, object> headers;
        
        private bool messageIdPresent;
        private string messageId;

        private bool priorityPresent;
        private byte priority;

        private bool replyToPresent;
        private string replyTo;

        private bool timestampPresent;
        private AmqpTimestamp timestamp;

        private bool typePresent;
        private string type;

        private bool userIdPresent;
        private string userId;



        public string AppId { get => appId; set => SetValue(out appId, out appIdPresent, value); }
        public string ClusterId { get => clusterId; set => SetValue(out clusterId, out clusterIdPresent, value); }
        public string ContentEncoding { get => contentEncoding; set => SetValue(out contentEncoding, out contentEncodingPresent, value); }
        public string ContentType { get => contentType; set => SetValue(out contentType, out contentTypePresent, value); }
        public string CorrelationId { get => correlationId; set => SetValue(out correlationId, out correlationIdPresent, value); }
        public byte DeliveryMode { get => deliveryMode; set => SetValue(out deliveryMode, out deliveryModePresent, value); }
        public string Expiration { get => expiration; set => SetValue(out expiration, out expirationPresent, value); }
        public IDictionary<string, object> Headers { get => headers; set => SetValue(out headers, out headersPresent, value); }
        public string MessageId { get => messageId; set => SetValue(out messageId, out messageIdPresent, value); }
        public bool Persistent { get; set; }
        public byte Priority { get => priority; set => SetValue(out priority, out priorityPresent, value); }
        public string ReplyTo { get => replyTo; set => SetValue(out replyTo, out replyToPresent, value); }
        public PublicationAddress ReplyToAddress { get; set; }
        public AmqpTimestamp Timestamp { get => timestamp; set => SetValue(out timestamp, out timestampPresent, value); }
        public string Type { get => type; set => SetValue(out type, out typePresent, value); }
        public string UserId { get => userId; set => SetValue(out userId, out userIdPresent, value); }


        private static void SetValue<T>(out T field, out bool present, T value)
        {
            field = value;
            present = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using Tapeti.Config;

namespace Tapeti.Default
{
    internal class RabbitMQMessageProperties : IMessageProperties
    {
        /// <summary>
        /// Provides access to the wrapped IBasicProperties
        /// </summary>
        public IBasicProperties BasicProperties { get; }


        /// <inheritdoc />
        public string ContentType
        {
            get => BasicProperties.IsContentTypePresent() ? BasicProperties.ContentType : null;
            set { if (!string.IsNullOrEmpty(value)) BasicProperties.ContentType = value; else BasicProperties.ClearContentType(); }
        }

        /// <inheritdoc />
        public string CorrelationId
        {
            get => BasicProperties.IsCorrelationIdPresent() ? BasicProperties.CorrelationId : null;
            set { if (!string.IsNullOrEmpty(value)) BasicProperties.CorrelationId = value; else BasicProperties.ClearCorrelationId(); }
        }

        /// <inheritdoc />
        public string ReplyTo
        {
            get => BasicProperties.IsReplyToPresent() ? BasicProperties.ReplyTo : null;
            set { if (!string.IsNullOrEmpty(value)) BasicProperties.ReplyTo = value; else BasicProperties.ClearReplyTo(); }
        }

        /// <inheritdoc />
        public bool? Persistent
        {
            get => BasicProperties.Persistent;
            set { if (value.HasValue) BasicProperties.Persistent = value.Value; else BasicProperties.ClearDeliveryMode(); }
        }

        /// <inheritdoc />
        public DateTime? Timestamp
        {
            get => DateTimeOffset.FromUnixTimeSeconds(BasicProperties.Timestamp.UnixTime).UtcDateTime;
            set
            {
                if (value.HasValue)
                    BasicProperties.Timestamp = new AmqpTimestamp(new DateTimeOffset(value.Value.ToUniversalTime()).ToUnixTimeSeconds());
                else
                    BasicProperties.ClearTimestamp();
            }
        }


        /// <inheritdoc />
        public RabbitMQMessageProperties(IBasicProperties basicProperties)
        {
            BasicProperties = basicProperties;
        }


        /// <inheritdoc />
        public RabbitMQMessageProperties(IBasicProperties basicProperties, IMessageProperties source)
        {
            BasicProperties = basicProperties;
            if (source == null)
                return;

            ContentType = source.ContentType;
            CorrelationId = source.CorrelationId;
            ReplyTo = source.ReplyTo;
            Persistent = source.Persistent;
            Timestamp = source.Timestamp;

            BasicProperties.Headers = null;
            foreach (var pair in source.GetHeaders())
                SetHeader(pair.Key, pair.Value);
        }


        /// <inheritdoc />
        public void SetHeader(string name, string value)
        {
            if (BasicProperties.Headers == null)
                BasicProperties.Headers = new Dictionary<string, object>();

            if (BasicProperties.Headers.ContainsKey(name))
                BasicProperties.Headers[name] = Encoding.UTF8.GetBytes(value);
            else
                BasicProperties.Headers.Add(name, Encoding.UTF8.GetBytes(value));
        }


        /// <inheritdoc />
        public string GetHeader(string name)
        {
            if (BasicProperties.Headers == null)
                return null;

            return BasicProperties.Headers.TryGetValue(name, out var value) ? Encoding.UTF8.GetString((byte[])value) : null;
        }


        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, string>> GetHeaders()
        {
            if (BasicProperties.Headers == null)
                yield break;

            foreach (var pair in BasicProperties.Headers)
                yield return new KeyValuePair<string, string>(pair.Key, Encoding.UTF8.GetString((byte[])pair.Value));
        }
    }
}

using System;
using System.Linq;
using System.Text;
using RabbitMQ.Client;
using Tapeti.Config;

namespace Tapeti.Default
{
    internal static class RabbitMQMessageProperties
    {
        /// <summary>
        /// </summary>
        public static MessageProperties ToMessageProperties(this IReadOnlyBasicProperties source)
        {
            var properties = new MessageProperties
            {
                ContentType = source.ContentType,
                CorrelationId = source.CorrelationId,
                ReplyTo = source.ReplyTo,
                Persistent = source.DeliveryMode == DeliveryModes.Persistent,
                Timestamp = source.IsTimestampPresent() ? DateTimeOffset.FromUnixTimeSeconds(source.Timestamp.UnixTime).UtcDateTime : null,
            };

            // ReSharper disable once InvertIf
            if (source.Headers is not null)
                foreach (var pair in source.Headers.Where(p => p.Value is not null))
                    properties.SetHeader(pair.Key, Encoding.UTF8.GetString((byte[])pair.Value!));

            return properties;
        }


        /// <summary>
        /// </summary>
        public static BasicProperties ToBasicProperties(this IMessageProperties source)
        {
            var headers = source.GetHeaders().ToDictionary(p => p.Key, object? (p) => Encoding.UTF8.GetBytes(p.Value));

            return new BasicProperties
            {
                ContentType = source.ContentType,
                CorrelationId = source.CorrelationId,
                ReplyTo = source.ReplyTo,
                Persistent = source.Persistent ?? false,
                Timestamp = source.Timestamp is not null ? new AmqpTimestamp(new DateTimeOffset(source.Timestamp.Value.ToUniversalTime()).ToUnixTimeSeconds()) : default,
                Headers = headers.Count > 0 ? headers : null
            };
        }
    }
}

using System;
using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Default
{
    /// <inheritdoc />
    /// <summary>
    /// IMessagePropertiesReader implementation for providing properties manually
    /// </summary>
    public class MessageProperties : IMessageProperties
    {
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();


        /// <inheritdoc />
        public string ContentType { get; set; }

        /// <inheritdoc />
        public string CorrelationId { get; set; }

        /// <inheritdoc />
        public string ReplyTo { get; set; }

        /// <inheritdoc />
        public bool? Persistent { get; set; }

        /// <inheritdoc />
        public DateTime? Timestamp { get; set; }


        /// <summary>
        /// </summary>
        public MessageProperties()
        {
        }


        /// <summary>
        /// </summary>
        public MessageProperties(IMessageProperties source)
        {
            if (source == null)
                return;

            ContentType = source.ContentType;
            CorrelationId = source.CorrelationId;
            ReplyTo = source.ReplyTo;
            Persistent = source.Persistent;
            Timestamp = source.Timestamp;

            headers.Clear();
            foreach (var pair in source.GetHeaders())
                SetHeader(pair.Key, pair.Value);
        }


        /// <inheritdoc />
        public void SetHeader(string name, string value)
        {
            if (headers.ContainsKey(name))
                headers[name] = value;
            else
                headers.Add(name, value);
        }

        /// <inheritdoc />
        public string GetHeader(string name)
        {
            return headers.TryGetValue(name, out var value) ? value : null;
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<string, string>> GetHeaders()
        {
            return headers;
        }
    }
}

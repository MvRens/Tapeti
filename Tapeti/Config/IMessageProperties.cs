using System;
using System.Collections.Generic;

namespace Tapeti.Config
{
    /// <summary>
    /// Metadata properties attached to a message, equivalent to the RabbitMQ Client's IBasicProperties.
    /// </summary>
    public interface IMessageProperties
    {
        /// <summary></summary>
        string? ContentType { get; set; }

        /// <summary></summary>
        string? CorrelationId { get; set; }

        /// <summary></summary>
        string? ReplyTo { get; set; }

        /// <summary></summary>
        bool? Persistent { get; set; }

        /// <summary></summary>
        DateTime? Timestamp { get; set; }


        /// <summary>
        /// Writes a custom header.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void SetHeader(string name, string value);


        /// <summary>
        /// Retrieves the value of a custom header field.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The value if found, null otherwise</returns>
        string? GetHeader(string name);


        /// <summary>
        /// Retrieves all custom headers.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> GetHeaders();
    }
}

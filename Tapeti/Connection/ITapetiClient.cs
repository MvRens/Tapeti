using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Connection
{
    /// <summary>
    /// Defines a queue binding to an exchange using a routing key
    /// </summary>
    public readonly struct QueueBinding : IEquatable<QueueBinding>
    {
        /// <summary></summary>
        public readonly string Exchange;

        /// <summary></summary>
        public readonly string RoutingKey;


        /// <summary>
        /// Initializes a new QueueBinding
        /// </summary>
        /// <param name="exchange"></param>
        /// <param name="routingKey"></param>
        public QueueBinding(string exchange, string routingKey)
        {
            Exchange = exchange;
            RoutingKey = routingKey;
        }


        /// <inheritdoc />
        public bool Equals(QueueBinding other)
        {
            return string.Equals(Exchange, other.Exchange) && string.Equals(RoutingKey, other.RoutingKey);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            return obj is QueueBinding other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Exchange != null ? Exchange.GetHashCode() : 0) * 397) ^ (RoutingKey != null ? RoutingKey.GetHashCode() : 0);
            }
        }

        /// <summary></summary>
        public static bool operator ==(QueueBinding left, QueueBinding right)
        {
            return left.Equals(right);
        }

        /// <summary></summary>
        public static bool operator !=(QueueBinding left, QueueBinding right)
        {
            return !left.Equals(right);
        }
    }


    /// <summary>
    /// Provides a bridge between Tapeti and the actual RabbitMQ client
    /// </summary>
    public interface ITapetiClient
    {


        /// <summary>
        /// Closes the connection to RabbitMQ gracefully.
        /// </summary>
        Task Close();
    }
}

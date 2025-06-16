using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using JetBrains.Annotations;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <summary>
    /// Defines the connection parameters.
    /// </summary>
    [PublicAPI]
    public class TapetiConnectionParams
    {
        private IDictionary<string, string>? clientProperties;
        private ushort publishChannelPoolSize;


        /// <summary>
        /// The hostname to connect to. Defaults to localhost.
        /// </summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// The port to connect to. Defaults to 5672.
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// The virtual host in RabbitMQ. Defaults to /.
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// The username to authenticate with. Defaults to guest.
        /// </summary>
        public string Username { get; set; } = "guest";

        /// <summary>
        /// The password to authenticate with. Defaults to guest.
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// The amount of message to prefetch. See http://www.rabbitmq.com/consumer-prefetch.html for more information.
        ///
        /// If set to 0, no limit will be applied.
        /// </summary>
        public ushort PrefetchCount { get; set; } = 50;

        /// <summary>
        /// The port the management plugin binds to. Only used when DeclareDurableQueues is enabled. Defaults to 15672.
        /// </summary>
        public int ManagementPort { get; set; } = 15672;

        /// <summary>
        /// The maximum number of consumers which are run concurrently.
        /// </summary>
        /// <remarks>
        /// The number of consumers is usually roughly equal to the number of queues consumed.
        /// Do not set too high to avoid overloading the thread pool.
        /// The RabbitMQ Client library defaults to 1. Due to older Tapeti versions implementing concurrency
        /// effectively limited by the PrefetchCount, this will default to Environment.ProcessorCount instead.
        /// </remarks>
        public ushort ConsumerDispatchConcurrency { get; set; }

        /// <summary>
        /// Key-value pair of properties that are set on the connection. These will be visible in the RabbitMQ Management interface.
        /// Note that you can either set a new dictionary entirely, to allow for inline declaration, or use this property directly
        /// and use the auto-created dictionary.
        /// </summary>
        /// <remarks>
        /// If any of the default keys used by the RabbitMQ Client library (product, version) are specified their value
        /// will be overwritten. See DefaultClientProperties in Connection.cs in the RabbitMQ .NET client source for the default values.
        /// </remarks>
        public IDictionary<string, string>? ClientProperties {
            get => clientProperties ??= new Dictionary<string, string>();
            set => clientProperties = value;
        }

        /// <summary>
        /// The number of channels reserved for publishing messages.
        /// </summary>
        /// <remarks>
        /// For each channel a corresponding task queue is allocated. This increases throughput at the cost of
        /// more running background tasks and possibly threads.<br/><br/>
        /// Defaults to <see cref="Environment.ProcessorCount"/> (up to 16 to account for diminishing returns).
        /// Setting this value to 0 will silently correct it to 1.
        /// </remarks>
        public ushort PublishChannelPoolSize
        {
            get => publishChannelPoolSize;
            set => publishChannelPoolSize = value == 0 ? (ushort)1 : value;
        }


        /// <summary>
        /// </summary>
        public TapetiConnectionParams()
        {
            var ushortProcessorCount = Environment.ProcessorCount <= ushort.MaxValue ? (ushort)Environment.ProcessorCount : ushort.MaxValue;

            ConsumerDispatchConcurrency = ushortProcessorCount;
            PublishChannelPoolSize = Math.Min(ushortProcessorCount, (ushort)16);
        }

        /// <summary>
        /// Construct a new TapetiConnectionParams instance based on standard URI syntax.
        /// </summary>
        /// <remarks>
        /// Supported query parameters are (case-insensitive):<br/>
        /// <ul>
        ///   <li>prefetchCount</li>
        ///   <li>managementPort</li>
        ///   <li>consumerDispatchConcurrency</li>
        ///   <li>publishChannelPoolSize</li>
        /// </ul>
        /// </remarks>
        /// <example>new TapetiConnectionParams(new Uri("amqp://username:password@hostname/"))</example>
        /// <example>new TapetiConnectionParams(new Uri("amqp://username:password@hostname:5672/virtualHost"))</example>
        /// <example>new TapetiConnectionParams(new Uri("amqp://username:password@hostname:5672/virtualHost?prefetchCount=50"))</example>
        /// <param name="uri"></param>
        public TapetiConnectionParams(Uri uri) : this()
        {
            HostName = uri.Host;
            VirtualHost = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;

            if (!uri.IsDefaultPort)
                Port = uri.Port;

            if (uri.UserInfo.Length > 0)
            {
                var userInfo = uri.UserInfo.Split(':').Select(Uri.UnescapeDataString).ToArray();
                if (userInfo.Length > 0)
                {
                    Username = userInfo[0];
                    if (userInfo.Length > 1)
                        Password = userInfo[1];
                }
            }

            if (string.IsNullOrEmpty(uri.Query))
                return;

            var query = HttpUtility.ParseQueryString(uri.Query);
            foreach (var key in query.AllKeys)
            {
                if (key is null)
                    continue;

                var value = query[key];
                if (value is null)
                    continue;

                switch (key.ToLowerInvariant())
                {
                    case "prefetchcount": PrefetchCount = ushort.Parse(value); break;
                    case "managementport": ManagementPort = int.Parse(value); break;
                    case "consumerdispatchconcurrency": ConsumerDispatchConcurrency = ushort.Parse(value); break;
                    case "publishchannelpoolsize": PublishChannelPoolSize = ushort.Parse(value); break;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <summary>
    /// Defines the connection parameters.
    /// </summary>
    public class TapetiConnectionParams
    {
        private IDictionary<string, string> clientProperties;


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
        /// Key-value pair of properties that are set on the connection. These will be visible in the RabbitMQ Management interface.
        /// Note that you can either set a new dictionary entirely, to allow for inline declaration, or use this property directly
        /// and use the auto-created dictionary.
        /// </summary>
        /// <remarks>
        /// If any of the default keys used by the RabbitMQ Client library (product, version) are specified their value
        /// will be overwritten. See DefaultClientProperties in Connection.cs in the RabbitMQ .NET client source for the default values.
        /// </remarks>
        public IDictionary<string, string> ClientProperties { 
            get => clientProperties ??= new Dictionary<string, string>();
            set => clientProperties = value;
        }


        /// <summary>
        /// </summary>
        public TapetiConnectionParams()
        {
        }

        /// <summary>
        /// Construct a new TapetiConnectionParams instance based on standard URI syntax.
        /// </summary>
        /// <example>new TapetiConnectionParams(new Uri("amqp://username:password@hostname/"))</example>
        /// <example>new TapetiConnectionParams(new Uri("amqp://username:password@hostname:5672/virtualHost"))</example>
        /// <param name="uri"></param>
        public TapetiConnectionParams(Uri uri)
        {
            HostName = uri.Host;
            VirtualHost = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;

            if (!uri.IsDefaultPort)
                Port = uri.Port;

            var userInfo = uri.UserInfo.Split(':');
            if (userInfo.Length <= 0) 
                return;

            Username = userInfo[0];
            if (userInfo.Length > 1)
                Password = userInfo[1];
        }
    }
}

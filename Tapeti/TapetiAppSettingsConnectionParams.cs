using System.Configuration;
using System.Linq;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    /// <inheritdoc />
    /// <summary>
    /// Implementation of TapetiConnectionParams which reads the values from the AppSettings.
    /// </summary>
    /// <list type="table">
    ///   <listheader>
    ///     <description>AppSettings keys</description>
    ///   </listheader>
    ///   <item><description>rabbitmq:hostname</description></item>
    ///   <item><description>rabbitmq:port</description></item>
    ///   <item><description>rabbitmq:virtualhost</description></item>
    ///   <item><description>rabbitmq:username</description></item>
    ///   <item><description>rabbitmq:password</description></item>
    ///   <item><description>rabbitmq:prefetchcount</description></item>
    ///   <item><description>rabbitmq:managementport</description></item>
    ///   <item><description>rabbitmq:clientproperty:*</description></item>
    /// </list>
    public class TapetiAppSettingsConnectionParams : TapetiConnectionParams
    {
        private const string DefaultPrefix = "rabbitmq:";
        // ReSharper disable InconsistentNaming
        private const string KeyHostname = "hostname";
        private const string KeyPort = "port";
        private const string KeyVirtualHost = "virtualhost";
        private const string KeyUsername = "username";
        private const string KeyPassword = "password";
        private const string KeyPrefetchCount = "prefetchcount";
        private const string KeyManagementPort = "managementport";
        private const string KeyClientProperty = "clientproperty:";
        // ReSharper restore InconsistentNaming


        private readonly struct AppSettingsKey
        {
            public readonly string Entry;
            public readonly string Parameter;

            public AppSettingsKey(string entry, string parameter)
            {
                Entry = entry;
                Parameter = parameter;
            }
        }


        /// <inheritdoc />
        /// <summary></summary>
        /// <param name="prefix">The prefix to apply to the keys. Defaults to "rabbitmq:"</param>
        public TapetiAppSettingsConnectionParams(string prefix = DefaultPrefix)
        {
            var keys = !string.IsNullOrEmpty(prefix)
                ? ConfigurationManager.AppSettings.AllKeys.Where(k => k.StartsWith(prefix)).Select(k => new AppSettingsKey(k, k.Substring(prefix.Length)))
                : ConfigurationManager.AppSettings.AllKeys.Select(k => new AppSettingsKey(k, k));

            

            foreach (var key in keys)
            {
                var value = ConfigurationManager.AppSettings[key.Entry];

                if (key.Parameter.StartsWith(KeyClientProperty))
                {
                    ClientProperties.Add(key.Parameter.Substring(KeyClientProperty.Length), value);
                }
                else
                {
                    // ReSharper disable once SwitchStatementMissingSomeCases - don't fail if we encounter an unknown value
                    switch (key.Parameter)
                    {
                        case KeyHostname: HostName = value; break;
                        case KeyPort: Port = int.Parse(value); break;
                        case KeyVirtualHost: VirtualHost = value; break;
                        case KeyUsername: Username = value; break;
                        case KeyPassword: Password = value; break;
                        case KeyPrefetchCount: PrefetchCount = ushort.Parse(value); break;
                        case KeyManagementPort: ManagementPort = int.Parse(value); break;
                    }
                }
            }
        }
    }
}

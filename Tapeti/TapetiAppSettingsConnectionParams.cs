using System;
using System.Configuration;
using System.Linq;

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
    /// </list>
    public class TapetiAppSettingsConnectionParams : TapetiConnectionParams
    {
        private const string DefaultPrefix = "rabbitmq:";
        private const string KeyHostname = "hostname";
        private const string KeyPort = "port";
        private const string KeyVirtualHost = "virtualhost";
        private const string KeyUsername = "username";
        private const string KeyPassword = "password";
        private const string KeyPrefetchCount = "prefetchcount";
        private const string KeyManagementPort = "managementport";


        /// <inheritdoc />
        /// <summary></summary>
        /// <param name="prefix">The prefix to apply to the keys. Defaults to "rabbitmq:"</param>
        public TapetiAppSettingsConnectionParams(string prefix = DefaultPrefix)
        {
            var keys = ConfigurationManager.AppSettings.AllKeys;

            void GetAppSetting(string key, Action<string> setValue)
            {
                if (keys.Contains(prefix + key)) setValue(ConfigurationManager.AppSettings[prefix + key]);
            }


            GetAppSetting(KeyHostname, value => HostName = value);
            GetAppSetting(KeyPort, value => Port = int.Parse(value));
            GetAppSetting(KeyVirtualHost, value => VirtualHost = value);
            GetAppSetting(KeyUsername, value => Username = value);
            GetAppSetting(KeyPassword, value => Password = value);
            GetAppSetting(KeyPrefetchCount, value => PrefetchCount = ushort.Parse(value));
            GetAppSetting(KeyManagementPort, value => ManagementPort = int.Parse(value));
        }
    }
}

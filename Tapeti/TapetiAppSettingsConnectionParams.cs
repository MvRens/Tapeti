using System;
using System.Configuration;
using System.Linq;

namespace Tapeti
{
    public class TapetiAppSettingsConnectionParams : TapetiConnectionParams
    {
        public const string DefaultPrefix = "rabbitmq:";
        public const string KeyHostname = "hostname";
        public const string KeyPort = "port";
        public const string KeyVirtualHost = "virtualhost";
        public const string KeyUsername = "username";
        public const string KeyPassword = "password";
        public const string KeyPrefetchCount = "prefetchcount";


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
        }
    }
}

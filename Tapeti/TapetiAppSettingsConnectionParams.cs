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
            Action<string, Action<string>> getAppSetting = (key, setValue) =>
            {
                if (keys.Contains(prefix + key))
                    setValue(ConfigurationManager.AppSettings[prefix + key]);
            };


            getAppSetting(KeyHostname, value => HostName = value);
            getAppSetting(KeyPort, value => Port = int.Parse(value));
            getAppSetting(KeyVirtualHost, value => VirtualHost = value);
            getAppSetting(KeyUsername, value => Username = value);
            getAppSetting(KeyPassword, value => Password = value);
            getAppSetting(KeyPrefetchCount, value => PrefetchCount = ushort.Parse(value));
        }
    }
}

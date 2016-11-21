using System;

namespace Tapeti
{
    public class TapetiConnectionParams
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";


        public TapetiConnectionParams()
        {            
        }

        public TapetiConnectionParams(Uri uri)
        {
            HostName = uri.Host;
            VirtualHost = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;

            if (!uri.IsDefaultPort)
                Port = uri.Port;

            var userInfo = uri.UserInfo.Split(':');
            if (userInfo.Length > 0)
            {
                Username = userInfo[0];
                if (userInfo.Length > 1)
                    Password = userInfo[1];
            }
        }
    }
}

using System;

namespace Tapeti.Default
{
    public class ConsoleLogger : ILogger
    {
        public void Connect(TapetiConnectionParams connectionParams)
        {
            Console.WriteLine($"[Tapeti] Connecting to {connectionParams.HostName}:{connectionParams.Port}{connectionParams.VirtualHost}");
        }

        public void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception)
        {
            Console.WriteLine($"[Tapeti] Connection failed: {exception}");
        }

        public void ConnectSuccess(TapetiConnectionParams connectionParams)
        {
            Console.WriteLine("[Tapeti] Connected");
        }

        public void HandlerException(Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}

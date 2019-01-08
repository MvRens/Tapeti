using System;
using ISeriLogger = Serilog.ILogger;

namespace Tapeti.Serilog
{
    public class TapetiSeriLogger: ILogger
    {
        private readonly ISeriLogger seriLogger;

        public TapetiSeriLogger(ISeriLogger seriLogger)
        {
            this.seriLogger = seriLogger;
        }

        public void Connect(TapetiConnectionParams connectionParams)
        {
            seriLogger.Information("Tapeti: trying to connect to {host}:{port}/{virtualHost}", 
                connectionParams.HostName,
                connectionParams.Port,
                connectionParams.VirtualHost);
        }

        public void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception)
        {
            seriLogger.Error(exception, "Tapeti: could not connect to {host}:{port}/{virtualHost}", 
                connectionParams.HostName,
                connectionParams.Port,
                connectionParams.VirtualHost);
        }

        public void ConnectSuccess(TapetiConnectionParams connectionParams)
        {
            seriLogger.Information("Tapeti: successfully connected to {host}:{port}/{virtualHost}", 
                connectionParams.HostName,
                connectionParams.Port,
                connectionParams.VirtualHost);
        }
        
        public void HandlerException(Exception e)
        {
            seriLogger.Error(e, "Tapeti: exception in message handler");
        }
    }
}

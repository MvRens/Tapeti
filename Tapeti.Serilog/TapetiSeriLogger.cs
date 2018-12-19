using System;
using ISeriLogger = Serilog.ILogger;

// ReSharper disable UnusedMember.Global

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
            // method not yet used in Tapeti
            seriLogger.Information("Trying to connected to " + connectionParams.HostName);
        }

        public void ConnectFailed(TapetiConnectionParams connectionParams)
        {
            // method not yet used in Tapeti
            seriLogger.Error("Could not connect to " + connectionParams.HostName);

        }

        public void ConnectSuccess(TapetiConnectionParams connectionParams)
        {
            // method not yet used in Tapeti
            seriLogger.Information("Succesfull connected to " + connectionParams.HostName);
        }
        
        public void HandlerException(Exception e)
        {
            seriLogger.Error(e, "Exception handled by Tapeti");
        }
    }
}

using System;

namespace Tapeti
{
    // This interface is deliberately specific and typed to allow for structured logging (e.g. Serilog)
    // instead of only string-based logging without control over the output.
    public interface ILogger
    {
        void Connect(TapetiConnectionParams connectionParams);
        void ConnectFailed(TapetiConnectionParams connectionParams);
        void ConnectSuccess(TapetiConnectionParams connectionParams);
        void HandlerException(Exception e);
    }
}

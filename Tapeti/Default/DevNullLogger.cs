using System;

namespace Tapeti.Default
{
    public class DevNullLogger : ILogger
    {
        public void Connect(TapetiConnectionParams connectionParams)
        {
        }

        public void ConnectFailed(TapetiConnectionParams connectionParams, Exception exception)
        {
        }

        public void ConnectSuccess(TapetiConnectionParams connectionParams)
        {
        }

        public void HandlerException(Exception e)
        {
        }
    }
}

using System;

namespace Tapeti.Default
{
    public class ConsoleLogger : ILogger
    {
        public void Connect(TapetiConnectionParams connectionParams)
        {
            throw new System.NotImplementedException();
        }

        public void ConnectFailed(TapetiConnectionParams connectionParams)
        {
            throw new System.NotImplementedException();
        }

        public void ConnectSuccess(TapetiConnectionParams connectionParams)
        {
            throw new System.NotImplementedException();
        }

        public void HandlerException(Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}

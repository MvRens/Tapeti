using System;
using Tapeti;

namespace Test
{
    public class MyLogger : ILogger
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
            Console.WriteLine("Mylogger: " + e.Message);
        }
    }
}

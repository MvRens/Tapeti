using System;
using Tapeti;

// ReSharper disable UnusedMember.Global

namespace Test
{
    public class MyLogger : ILogger
    {
        public void Connect(TapetiConnectionParams connectionParams)
        {
            throw new NotImplementedException();
        }

        public void ConnectFailed(TapetiConnectionParams connectionParams)
        {
            throw new NotImplementedException();
        }

        public void ConnectSuccess(TapetiConnectionParams connectionParams)
        {
            throw new NotImplementedException();
        }

        public void HandlerException(Exception e)
        {
            Console.WriteLine("Mylogger: " + e.Message);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tapeti;

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

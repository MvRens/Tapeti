using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti.Connection
{
    public interface IConnectionEventListener
    {
        void Connected();
        void Reconnected();
        void Disconnected();
    }
}

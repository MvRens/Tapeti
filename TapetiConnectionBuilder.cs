using System;

namespace Tapeti
{
    public class TapetiConnectionBuilder
    {
        public IConnection Build()
        {
            throw new NotImplementedException();
        }


        public TapetiConnectionBuilder SetExchange(string exchange)
        {
            return this;
        }


        public TapetiConnectionBuilder SetDependencyResolver(IDependencyResolver dependencyResolver)
        {
            return this;
        }


        public TapetiConnectionBuilder SetTopology(ITopology topology)
        {
            return this;
        }
    }
}

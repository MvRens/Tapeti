using System;
using Tapeti.Annotations;
using Tapeti.Flow;

namespace Test
{
    [MessageController]
    [DynamicQueue]
    public class FlowEndController
    {
        private readonly IFlowProvider flowProvider;

        public FlowEndController(IFlowProvider flowProvider)
        {
            this.flowProvider = flowProvider;
        }

        public IYieldPoint StartFlow(PingMessage message)
        {
            Console.WriteLine("PingMessage received, call flowProvider.End()");
            return Finish();
        }


        private IYieldPoint Finish()
        {
            return flowProvider.End();
        }


        public class PingMessage
        {

        }

    }
}

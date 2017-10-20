using System;
using Tapeti.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.Annotations;

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
            Console.WriteLine("PingMessage received, calling flowProvider.End() directly");

            if (DateTime.Now < new DateTime(2000, 1, 1))
            {
                //never true
                return flowProvider
                    .YieldWithRequestSync<PingConfirmationRequestMessage, PingConfirmationResponseMessage>
                    (new PingConfirmationRequestMessage() { StoredInState = "Ping:" },
                        HandlePingConfirmationResponse);
            }

            return Finish();
        }


        [Continuation]
        public IYieldPoint HandlePingConfirmationResponse(PingConfirmationResponseMessage msg)
        {
            Console.WriteLine("Ending ping flow: " + msg.Answer);
            return Finish();
        }


        private IYieldPoint Finish()
        {
            return flowProvider.End();
        }


        public class PingMessage
        {

        }

        [Request(Response = typeof(PingConfirmationResponseMessage))]
        public class PingConfirmationRequestMessage
        {
            public string StoredInState { get; set; }
        }


        public class PingConfirmationResponseMessage
        {
            public string Answer { get; set; }
        }

        public PingConfirmationResponseMessage PingConfirmation(PingConfirmationRequestMessage message)
        {
            Console.WriteLine(">> receive Ping (returning pong)");

            return new PingConfirmationResponseMessage
            {
                Answer = message.StoredInState + " Pong!"
            };
        }
    }
}

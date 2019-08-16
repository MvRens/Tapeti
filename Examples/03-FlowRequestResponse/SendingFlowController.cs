using System;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.Annotations;

namespace _03_FlowRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.03")]
    public class SendingFlowController
    {
        private readonly IFlowProvider flowProvider;
        private readonly IExampleState exampleState;


        // Shows how multiple values can be passed to a start method
        public struct StartData
        {
            public DateTime RequestStartTime;
            public int Amount;
        }

        // Private and protected fields are lost between method calls because the controller is
        // recreated when a response arrives. When using a persistent flow repository this may
        // even be after a restart of the application.
        private bool nonPersistentState;


        // Public fields will be stored.
        public DateTime RequestStartTime;


        public SendingFlowController(IFlowProvider flowProvider, IExampleState exampleState)
        {
            this.flowProvider = flowProvider;
            this.exampleState = exampleState;
        }


        [Start]
        public IYieldPoint StartFlow(StartData startData)
        {
            nonPersistentState = true;
            RequestStartTime = startData.RequestStartTime;

            return flowProvider.YieldWithRequestSync<QuoteRequestMessage, QuoteResponseMessage>(
                new QuoteRequestMessage
                {
                    Amount = startData.Amount
                },
                HandleQuoteResponse);
        }


        [Continuation]
        public IYieldPoint HandleQuoteResponse(QuoteResponseMessage message)
        {
            if (nonPersistentState)
                Console.WriteLine("This is not supposed to show. NonPersistentState should not be retained. Someone please check http://www.hasthelargehadroncolliderdestroyedtheworldyet.com.");

            Console.WriteLine("Request start: " + RequestStartTime.ToLongTimeString());
            Console.WriteLine("Response time: " + DateTime.Now.ToLongTimeString());
            Console.WriteLine("Quote: " + message.Quote);


            exampleState.Done();

            return flowProvider.End();
        }
    }
}

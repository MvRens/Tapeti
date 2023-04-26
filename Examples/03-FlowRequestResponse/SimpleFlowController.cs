using System;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Config.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.Annotations;

namespace _03_FlowRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.03")]
    public class SimpleFlowController
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

        
        // Be sure not to accidentally use any public fields that aren't serializable, for example:
        //public TaskCompletionSource<bool> SerializationFail = new TaskCompletionSource<bool>();
        //
        // In the Newtonsoft.Json version at the time of writing, this will not result in an exception but instead hang the flow!


        public SimpleFlowController(IFlowProvider flowProvider, IExampleState exampleState)
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
                Console.WriteLine("[SimpleFlowController] This is not supposed to show. NonPersistentState should not be retained. Someone please check http://www.hasthelargehadroncolliderdestroyedtheworldyet.com.");

            Console.WriteLine("[SimpleFlowController] Request start: " + RequestStartTime.ToLongTimeString());
            Console.WriteLine("[SimpleFlowController] Response time: " + DateTime.Now.ToLongTimeString());
            Console.WriteLine("[SimpleFlowController] Quote: " + message.Quote);

            
            // Test for issue #21: Same request/response twice in flow does not continue
            return flowProvider.YieldWithRequestSync<QuoteRequestMessage, QuoteResponseMessage>(
                new QuoteRequestMessage
                {
                    Amount = 42
                },
                HandleQuoteResponse2);

            
            //exampleState.Done();
            //return flowProvider.End();
        }



        [Continuation]
        public IYieldPoint HandleQuoteResponse2(QuoteResponseMessage message)
        {
            Console.WriteLine("[SimpleFlowController] Quote 2: " + message.Quote);

            exampleState.Done();

            return flowProvider.End();
        }

    }
}

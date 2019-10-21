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
    public class ParallelFlowController
    {
        private readonly IFlowProvider flowProvider;
        private readonly IExampleState exampleState;

        public string FirstQuote;
        public string SecondQuote;


        public ParallelFlowController(IFlowProvider flowProvider, IExampleState exampleState)
        {
            this.flowProvider = flowProvider;
            this.exampleState = exampleState;
        }


        [Start]
        public IYieldPoint StartFlow()
        {
            return flowProvider.YieldWithParallelRequest()
                .AddRequestSync<QuoteRequestMessage, QuoteResponseMessage>(
                    new QuoteRequestMessage
                    {
                        Amount = 1
                    },
                    HandleFirstQuoteResponse)
                .AddRequestSync<QuoteRequestMessage, QuoteResponseMessage>(
                    new QuoteRequestMessage
                    {
                        Amount = 2
                    },
                    HandleSecondQuoteResponse)
                .YieldSync(AllQuotesReceived);
        }


        [Continuation]
        public void HandleFirstQuoteResponse(QuoteResponseMessage message)
        {
            Console.WriteLine("[ParallelFlowController] First quote response received");
            FirstQuote = message.Quote;
        }


        [Continuation]
        public void HandleSecondQuoteResponse(QuoteResponseMessage message)
        {
            Console.WriteLine("[ParallelFlowController] Second quote response received");
            SecondQuote = message.Quote;
        }


        private IYieldPoint AllQuotesReceived()
        {
            Console.WriteLine("[ParallelFlowController] First quote: " + FirstQuote);
            Console.WriteLine("[ParallelFlowController] Second quote: " + SecondQuote);

            exampleState.Done();
            return flowProvider.End();
        }
    }
}

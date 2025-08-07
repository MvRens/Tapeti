using System;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Config.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.Annotations;

namespace _03_FlowRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.03.parallel")]
    public class ParallelFlowController
    {
        private readonly IFlowProvider flowProvider;
        private readonly IExampleState exampleState;

        public string? FirstQuote;
        public string? SecondQuote;
        public string? ThirdQuote;


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
                .AddRequest<QuoteRequestMessage, QuoteResponseMessage>(
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
        public async ValueTask HandleSecondQuoteResponse(QuoteResponseMessage message, IFlowParallelRequest parallelRequest)
        {
            Console.WriteLine("[ParallelFlowController] Second quote response received");
            SecondQuote = message.Quote;

            // Example of adding a request to an ongoing parallel request
            await parallelRequest.AddRequestSync<QuoteRequestMessage, QuoteResponseMessage>(
                new QuoteRequestMessage
                {
                    Amount = 3
                },
                HandleThirdQuoteResponse);
        }


        [Continuation]
        public void HandleThirdQuoteResponse(QuoteResponseMessage message)
        {
            Console.WriteLine("[ParallelFlowController] First quote response received");
            ThirdQuote = message.Quote;
        }


        private IYieldPoint AllQuotesReceived()
        {
            Console.WriteLine("[ParallelFlowController] First quote: " + FirstQuote);
            Console.WriteLine("[ParallelFlowController] Second quote: " + SecondQuote);
            Console.WriteLine("[ParallelFlowController] Third quote: " + ThirdQuote);

            return flowProvider.YieldWithParallelRequest()
                .YieldSync(ImmediateConvergeTest, FlowNoRequestsBehaviour.Converge);
        }


        private IYieldPoint ImmediateConvergeTest()
        {
            Console.WriteLine("[ParallelFlowController] Second parallel flow immediately converged");

            exampleState.Done();
            return flowProvider.End();
        }
    }
}

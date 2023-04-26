using System;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Config.Annotations;

namespace _06_StatelessRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.06")]
    public class ExampleMessageController
    {
        private readonly IExampleState exampleState;


        public ExampleMessageController(IExampleState exampleState)
        {
            this.exampleState = exampleState;
        }


        [ResponseHandler]
        public void HandleQuoteResponse(QuoteResponseMessage message)
        {
            Console.WriteLine("Received response: " + message.Quote);
            exampleState.Done();
        }
    }
}

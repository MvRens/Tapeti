using System.Threading.Tasks;
using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _03_FlowRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.03")]
    public class ReceivingMessageController
    {
        // No publisher required, responses can simply be returned
        public async Task<QuoteResponseMessage> HandleQuoteRequest(QuoteRequestMessage message)
        {
            string quote;

            switch (message.Amount)
            {
                case 1:
                    // Well, they asked for it... :-)
                    quote = "'";
                    break;
                
                case 2:
                    quote = "\"";
                    break;

                default:
                    // We have to return a response.
                    quote = null;
                    break;
            }

            // Just gonna let them wait for a bit, to demonstrate async message handlers
            await Task.Delay(1000);

            return new QuoteResponseMessage
            {
                Quote = quote
            };
        }
    }
}

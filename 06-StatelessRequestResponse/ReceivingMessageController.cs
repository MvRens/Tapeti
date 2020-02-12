using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _06_StatelessRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.06.receiver")]
    public class ReceivingMessageController
    {
        // No publisher required, responses can simply be returned
        public QuoteResponseMessage HandleQuoteRequest(QuoteRequestMessage message)
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

            return new QuoteResponseMessage
            {
                Quote = quote
            };
        }
    }
}

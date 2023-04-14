using Messaging.TapetiExample;
using Tapeti.Config.Annotations;

namespace _06_StatelessRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.06.receiver")]
    public class ReceivingMessageController
    {
        // No publisher required, responses can simply be returned
        public static QuoteResponseMessage HandleQuoteRequest(QuoteRequestMessage message)
        {
            var quote = message.Amount switch
            {
                1 =>
                    // Well, they asked for it... :-)
                    "'",
                2 => "\"",
                _ => null
            };

            return new QuoteResponseMessage
            {
                Quote = quote
            };
        }
    }
}

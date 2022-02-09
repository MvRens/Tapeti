using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _06_StatelessRequestResponse
{
    [MessageController]
    [DynamicQueue("tapeti.example.06.receiver")]
    public class ReceivingMessageController
    {
        // No publisher required, responses can simply be returned
        #pragma warning disable CA1822 // Mark members as static - not supported yet by Tapeti
        public QuoteResponseMessage HandleQuoteRequest(QuoteRequestMessage message)
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
        #pragma warning restore CA1822
    }
}

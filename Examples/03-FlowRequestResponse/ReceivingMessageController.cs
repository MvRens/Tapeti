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
        #pragma warning disable CA1822 // Mark members as static - not supported yet by Tapeti
        public async Task<QuoteResponseMessage> HandleQuoteRequest(QuoteRequestMessage message)
        {
            var quote = message.Amount switch
            {
                1 =>
                    // Well, they asked for it... :-)
                    "'",
                2 => "\"",
                _ => new string('\'', message.Amount)
            };

            // Just gonna let them wait for a bit, to demonstrate async message handlers
            await Task.Delay(1000);

            return new QuoteResponseMessage
            {
                Quote = quote
            };
        }
        #pragma warning restore CA1822
    }
}

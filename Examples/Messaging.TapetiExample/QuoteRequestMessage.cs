using Tapeti.Annotations;

namespace Messaging.TapetiExample
{
    [Request(Response = typeof(QuoteResponseMessage))]
    public class QuoteRequestMessage
    {
        public int Amount { get; set; }
    }


    public class QuoteResponseMessage
    {
        public string Quote { get; set; }
    }
}

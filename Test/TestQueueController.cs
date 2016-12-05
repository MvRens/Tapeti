using System;
using Tapeti;
using Tapeti.Annotations;

namespace Test
{
    [DynamicQueue]
    public class TestQueueController : MessageController
    {
        private readonly IPublisher publisher;


        public TestQueueController(IPublisher publisher)
        {
            this.publisher = publisher;
        }

       
        public PoloMessage Marco(MarcoMessage message)
        {
            Console.WriteLine("Marco!");
            return new PoloMessage();
        }


        public void Polo(PoloMessage message)
        {
            Console.WriteLine("Polo!");
        }
    }


    public class MarcoMessage
    {
    }


    public class PoloMessage
    {
    }
}

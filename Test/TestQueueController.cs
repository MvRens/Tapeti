using System;
using System.Threading.Tasks;
using Tapeti;
using Tapeti.Annotations;

namespace Test
{
    //[Exchange("myexchange")]
    //[Queue("staticqueue")]
    [Queue]
    public class TestQueueController
    {
        private readonly IPublisher publisher;


        public TestQueueController(IPublisher publisher)
        {
            this.publisher = publisher;
        }

       
        public async Task Marco(MarcoMessage message)
        {
            Console.WriteLine("Marco!");
            await publisher.Publish(new PoloMessage());
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

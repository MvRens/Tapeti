using System.Threading.Tasks;
using Messaging.TapetiExample;
using Tapeti;

namespace _01_PublishSubscribe
{
    public class ExamplePublisher
    {
        private readonly IPublisher publisher;

        /// <summary>
        /// Shows that the IPublisher is registered in the container by Tapeti
        /// </summary>
        /// <param name="publisher"></param>
        public ExamplePublisher(IPublisher publisher)
        {
            this.publisher = publisher;
        }


        public async Task SendTestMessage()
        {
            await publisher.Publish(new PublishSubscribeMessage
            {
                Greeting = "Hello world of messaging!"
            });
        }
    }
}

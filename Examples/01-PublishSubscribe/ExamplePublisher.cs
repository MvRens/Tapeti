using System;
using System.ComponentModel.DataAnnotations;
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


            // Demonstrates what happens when DataAnnotations is enabled 
            // and the message is invalid
            try
            {
                await publisher.Publish(new PublishSubscribeMessage());

                Console.WriteLine("This is not supposed to show. Did you disable the DataAnnotations extension?");
            }
            catch (ValidationException e)
            {
                Console.WriteLine("As expected, the DataAnnotations check failed: " + e.Message);
            }
        }
    }
}

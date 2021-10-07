using System;
using System.Threading.Tasks;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _08_MessageHandlerLogging
{
    [MessageController]
    [DynamicQueue("tapeti.example.08.slow")]
    public class SlowMessageController
    {
        private readonly IExampleState exampleState;


        public SlowMessageController(IExampleState exampleState)
        {
            this.exampleState = exampleState;
        }


        public async Task GimmeASec(PublishSubscribeMessage message)
        {
            Console.WriteLine("Received message (in slow controller): " + message.Greeting);

            await Task.Delay(1000);
            
            exampleState.Done();
        }
    }
}
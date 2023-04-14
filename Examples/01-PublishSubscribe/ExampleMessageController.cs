using System;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Config.Annotations;

namespace _01_PublishSubscribe
{
    [MessageController]
    [DynamicQueue("tapeti.example.01")]
    public class ExampleMessageController
    {
        private readonly IExampleState exampleState;


        public ExampleMessageController(IExampleState exampleState)
        {
            this.exampleState = exampleState;
        }


        public void HandlePublishSubscribeMessage(PublishSubscribeMessage message)
        {
            Console.WriteLine("Received message: " + message.Greeting);
            exampleState.Done();
        }
    }
}

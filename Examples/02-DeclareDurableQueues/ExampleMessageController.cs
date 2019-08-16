using System;
using ExampleLib;
using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _02_DeclareDurableQueues
{
    [MessageController]
    [DurableQueue("tapeti.example.02")]
    public class ExampleMessageController
    {
        private readonly IExampleState exampleState;


        public ExampleMessageController(IExampleState exampleState)
        {
            this.exampleState = exampleState;
        }


        public void HandlePublishSubscribeMessage(PublishSubscribeMessage message)
        {
            // Note that if you run example 01 after 02, it's message will also be in this durable queue
            Console.WriteLine("Received message: " + message.Greeting);
            exampleState.Done();
        }
    }
}

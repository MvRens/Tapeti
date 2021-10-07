using System;
using Messaging.TapetiExample;
using Tapeti.Annotations;
using Tapeti.Serilog;

namespace _08_MessageHandlerLogging
{
    [MessageController]
    [DynamicQueue("tapeti.example.08.speedy")]
    public class SpeedyMessageController
    {
        // ReSharper disable once InconsistentNaming
        public void IAmSpeed(PublishSubscribeMessage message, IDiagnosticContext diagnosticContext)
        {
            diagnosticContext.Set("PropertySetByMessageHandler", 42);
            
            Console.WriteLine("Received message (in speedy controller): " + message.Greeting);
        }
    }
}
using Messaging.TapetiExample;
using Tapeti.Config.Annotations;

namespace _05_SpeedTest
{
    [MessageController]
    [DynamicQueue("tapeti.example.05")]
    public class SpeedMessageController
    {
        private readonly IMessageCounter messageCounter;

        public SpeedMessageController(IMessageCounter messageCounter)
        {
            this.messageCounter = messageCounter;
        }


        #pragma warning disable IDE0060 // Remove unused parameter
        public void HandleSpeedTestMessage(SpeedTestMessage message)
        {
            messageCounter.Add();
        }
        #pragma warning restore IDE0060
    }
}

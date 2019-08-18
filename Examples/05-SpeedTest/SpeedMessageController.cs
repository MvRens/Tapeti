using Messaging.TapetiExample;
using Tapeti.Annotations;

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


        public void HandleSpeedTestMessage(SpeedTestMessage message)
        {
            messageCounter.Add();
        }
    }
}

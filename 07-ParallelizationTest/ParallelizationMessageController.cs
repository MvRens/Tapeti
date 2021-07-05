using System.Threading.Tasks;
using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _07_ParallelizationTest
{
    [MessageController]
    [DynamicQueue("tapeti.example.07")]
    public class ParallelizationMessageController
    {
        private readonly IMessageParallelization messageParallelization;

        public ParallelizationMessageController(IMessageParallelization messageParallelization)
        {
            this.messageParallelization = messageParallelization;
        }


        public async Task HandleSpeedTestMessage(SpeedTestMessage message)
        {
            await messageParallelization.WaitForBatch();
        }
    }
}

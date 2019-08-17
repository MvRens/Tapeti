using System;
using System.Threading.Tasks;
using Messaging.TapetiExample;
using Tapeti.Annotations;

namespace _04_Transient
{
    [MessageController]
    [DynamicQueue("tapeti.example.04")]
    public class UsersMessageController
    {
        // No publisher required, responses can simply be returned
        public async Task<LoggedInUsersResponseMessage> HandleQuoteRequest(LoggedInUsersRequestMessage message)
        {
            // Simulate the response taking some time
            await Task.Delay(1000);

            return new LoggedInUsersResponseMessage
            {
                Count = new Random().Next(0, 100)
            };
        }
    }
}

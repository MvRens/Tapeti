using Tapeti.Annotations;

namespace Messaging.TapetiExample
{
    [Request(Response = typeof(LoggedInUsersResponseMessage))]
    public class LoggedInUsersRequestMessage
    {
    }


    public class LoggedInUsersResponseMessage
    {
        public int Count { get; set; }
    }
}

using System.Threading.Tasks;
using Tapeti.Config.Annotations;

namespace Tapeti.Tests.Client.Controller
{
    [Annotations.Request(Response = typeof(FilteredResponseMessage))]
    public class FilteredRequestMessage
    {
        public int ExpectedHandler { get; set; }
    }

    public class FilteredResponseMessage
    {
        public int ExpectedHandler { get; set; }
    }


    #pragma warning disable CA1822 // Mark members as static
    [MessageController]
    [DurableQueue("request.response.filter")]
    public class RequestResponseFilterController
    {
        public static TaskCompletionSource<int> ValidResponse { get; private set; } = new();
        public static TaskCompletionSource<int> InvalidResponse { get; private set; } = new();


        public FilteredResponseMessage EchoRequest(FilteredRequestMessage message)
        {
            return new FilteredResponseMessage
            {
                ExpectedHandler = message.ExpectedHandler
            };
        }


        [NoBinding]
        public static void ResetCompletionSource()
        {
            ValidResponse = new TaskCompletionSource<int>();
            InvalidResponse = new TaskCompletionSource<int>();
        }



        [ResponseHandler]
        public void Handler1(FilteredResponseMessage message)
        {
            if (message.ExpectedHandler != 1)
                InvalidResponse.TrySetResult(1);
            else
                ValidResponse.SetResult(1);
        }


        [ResponseHandler]
        public void Handler2(FilteredResponseMessage message)
        {
            if (message.ExpectedHandler != 2)
                InvalidResponse.TrySetResult(2);
            else
                ValidResponse.SetResult(2);
        }
    }
    #pragma warning restore CA1822
}

using System;
using System.Threading.Tasks;
using Tapeti;
using Tapeti.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.Annotations;

namespace Test
{
    [DynamicQueue]
    public class MarcoController : MessageController
    {
        private readonly IPublisher publisher;
        private readonly IFlowProvider flowProvider;
        private readonly Visualizer visualizer;

        // Public properties are automatically stored and retrieved while in a flow
        public Guid StateTestGuid;


        public MarcoController(IPublisher publisher, IFlowProvider flowProvider, Visualizer visualizer)
        {
            this.publisher = publisher;
            this.flowProvider = flowProvider;
            this.visualizer = visualizer;
        }


        /**
         * The Visualizer could've been injected through the constructor, which is
         * the recommended way. Just testing the injection middleware here.
         */
        public async Task<IYieldPoint> Marco(MarcoMessage message, Visualizer myVisualizer)
        {
            Console.WriteLine(">> Marco (yielding with request)");

            await myVisualizer.VisualizeMarco();

            return flowProvider.YieldWithRequestSync<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>(
                new PoloConfirmationRequestMessage()
                {
                    StoredInState = StateTestGuid
                },
                HandlePoloConfirmationResponse);
        }


        [Continuation]
        public IYieldPoint HandlePoloConfirmationResponse(PoloConfirmationResponseMessage message)
        {
            Console.WriteLine(">> HandlePoloConfirmationResponse (ending flow)");

            Console.WriteLine(message.ShouldMatchState.Equals(StateTestGuid) ? "Confirmed!" : "Oops! Mismatch!");

            // This should error, as MarcoMessage expects a PoloMessage as a response
            return flowProvider.EndWithResponse(new PoloMessage());
        }


        /**
         * For simple request response patterns, the return type can be used.
         * This will automatically include the correlationId in the response and
         * use the replyTo header of the request if provided.
         */

        // TODO validation middleware to ensure a request message returns the specified response (already done for IYieldPoint methods)
        public PoloConfirmationResponseMessage PoloConfirmation(PoloConfirmationRequestMessage message)
        {
            Console.WriteLine(">> PoloConfirmation (returning confirmation)");

            return new PoloConfirmationResponseMessage
            {
                ShouldMatchState = message.StoredInState
            };
        }



        public void Polo(PoloMessage message)
        {
            Console.WriteLine(">> Polo");
            StateTestGuid = Guid.NewGuid();
        }
    }


    [Request(Response = typeof(PoloMessage))]
    public class MarcoMessage
    {
    }


    public class PoloMessage
    {
    }


    [Request(Response = typeof(PoloConfirmationResponseMessage))]
    public class PoloConfirmationRequestMessage
    {
        public Guid StoredInState { get; set; }
    }


    public class PoloConfirmationResponseMessage
    {
        public Guid ShouldMatchState { get; set; }
    }
}

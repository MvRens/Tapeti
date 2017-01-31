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
        public async Task Marco(MarcoMessage message, Visualizer myVisualizer)
        {
            await myVisualizer.VisualizeMarco();
            await publisher.Publish(new PoloMessage());
        }


        public IYieldPoint Polo(PoloMessage message)
        {
            StateTestGuid = Guid.NewGuid();

            return flowProvider.YieldWithRequest<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>(
                new PoloConfirmationRequestMessage()
                {
                    StoredInState = StateTestGuid
                }, 
                HandlePoloConfirmationResponse);
        }


        public async Task<IYieldPoint> HandlePoloConfirmationResponse(PoloConfirmationResponseMessage message)
        {
            await visualizer.VisualizePolo(message.ShouldMatchState.Equals(StateTestGuid));
            return flowProvider.End();
        }


        /**
         * For simple request response patterns, the return type can be used.
         * This will automatically include the correlationId in the response and
         * use the replyTo header of the request if provided.
         */

        public PoloConfirmationResponseMessage PoloConfirmation(PoloConfirmationRequestMessage message)
        {
            return new PoloConfirmationResponseMessage
            {
                ShouldMatchState = message.StoredInState
            };
        }
    }


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

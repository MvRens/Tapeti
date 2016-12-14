using System.Threading.Tasks;
using Tapeti;
using Tapeti.Annotations;
using Tapeti.Saga;

namespace Test
{
    [DynamicQueue]
    public class MarcoController : MessageController
    {
        private readonly IPublisher publisher;
        private readonly ISagaProvider sagaProvider;


        public MarcoController(IPublisher publisher, ISagaProvider sagaProvider)
        {
            this.publisher = publisher;
            this.sagaProvider = sagaProvider;
        }


        /*
         * For simple request response patterns, the return type can also be used:
        
        public async Task<PoloMessage> Marco(MarcoMessage message, Visualizer visualizer)
        {
            visualizer.VisualizeMarco();
            return new PoloMessage(); ;
        }
        */

        // Visualizer can also be constructor injected, just proving a point here...
        public async Task Marco(MarcoMessage message, Visualizer visualizer)
        {
            visualizer.VisualizeMarco();

            using (var saga = await sagaProvider.Begin(new MarcoPoloSaga()))
            {
                // TODO provide publish extension with Saga support
                await publisher.Publish(new PoloMessage(), saga);
            }
        }


        public void Polo(PoloMessage message, Visualizer visualizer, ISaga<MarcoPoloSaga> saga)
        {
            if (saga.State.ReceivedPolo)
                return;

            saga.State.ReceivedPolo = true;
            visualizer.VisualizePolo();
        }


        /*
        [CallID("eerste")] 
        Implicit:
       
        using (sagaProvider.Continue(correlatieID))
        {
          saga refcount--;
        public void PoloColorResponse1(PoloColorResponse message, ISaga<MarcoState> saga)
        {
            
            saga.State == MarcoState



            state.Color = message.Color;

            if (state.Complete)
            {
                publisher.Publish(new PoloMessage());
            }
        }
        */
    }


    public class MarcoMessage
    {
    }


    public class PoloMessage
    {
    }


    public class MarcoPoloSaga
    {
        public bool ReceivedPolo;
    }
}

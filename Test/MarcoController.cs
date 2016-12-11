using System;
using Microsoft.SqlServer.Server;
using Tapeti;
using Tapeti.Annotations;

namespace Test
{
    [DynamicQueue]
    public class MarcoController : MessageController
    {
        private readonly IPublisher publisher;


        public MarcoController(IPublisher publisher/*, ISagaProvider sagaProvider*/)
        {
            this.publisher = publisher;
        }


        //[StaticQueue("test")]
        public PoloMessage Marco(MarcoMessage message)
        {
            /*
            using (sagaProvider.Begin<MarcoState>(new MarcoState
            {
                ...
            }))
            {
                //publisher.Publish(new PoloColorRequest(), saga, PoloColorResponse1);
                //publisher.Publish(new PoloColorRequest(), saga, callID = "tweede");

                // Saga refcount = 2
            }
            */

            return new PoloMessage(); ;
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

        public void Polo(PoloMessage message)
        {
            Console.WriteLine("Polo!");
        }
    }


    public class MarcoMessage
    {
    }


    public class PoloMessage
    {
    }




    public class PoloColorRequest
    {

    }

    public class PoloColorResponse
    {

    }
}

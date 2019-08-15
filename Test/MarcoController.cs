using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Tapeti.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.Annotations;

// ReSharper disable UnusedMember.Global

namespace Test
{
    [MessageController]
    [DynamicQueue]
    public class MarcoController
    {
        //private readonly IPublisher publisher;
        private readonly IFlowProvider flowProvider;
        //private readonly Visualizer visualizer;

        // Public properties are automatically stored and retrieved while in a flow
        public Guid StateTestGuid { get; set; }

        public int Phase;

        public MarcoController(/*IPublisher publisher, */IFlowProvider flowProvider/*, Visualizer visualizer*/)
        {
            //this.publisher = publisher;
            this.flowProvider = flowProvider;
            //this.visualizer = visualizer;
        }


        [Start]
        public async Task<IYieldPoint> StartFlow(bool go)
        {
            Console.WriteLine("Phase = " + Phase + " Starting stand-alone flow");
            await Task.Delay(10);

            Phase = 1;

            if (go)
                return flowProvider.YieldWithRequestSync<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>
                    (new PoloConfirmationRequestMessage(),
                    HandlePoloConfirmationResponse);

            Console.WriteLine("Phase = " + Phase + " Ending stand-alone flow prematurely");
            return flowProvider.End();
        }


        [Continuation]
        public IYieldPoint HandlePoloConfirmationResponse(PoloConfirmationResponseMessage msg)
        {
            Console.WriteLine("Phase = " + Phase + " Handling the first response and sending the second...");

            Phase = 2;

            return flowProvider.YieldWithRequestSync<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>
                    (new PoloConfirmationRequestMessage(),
                    HandlePoloConfirmationResponseEnd);
        }


        [Continuation]
        public IYieldPoint HandlePoloConfirmationResponseEnd(PoloConfirmationResponseMessage msg)
        {
            Console.WriteLine("Phase = " + Phase + " Handling the second response and Ending stand-alone flow");
            return flowProvider.End();
        }


        [Start]
        public IYieldPoint TestParallelRequest()
        {
            Console.WriteLine(">> Marco (yielding with request)");

            StateTestGuid = Guid.NewGuid();
            Console.WriteLine($"Starting parallel request with StateTestGuid {StateTestGuid}");

            return flowProvider.YieldWithParallelRequest()
                .AddRequestSync<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>(new PoloConfirmationRequestMessage
                {
                    StoredInState = StateTestGuid,
                    EnumValue = TestEnum.Value1

                }, HandlePoloConfirmationResponse1)

                .AddRequestSync<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>(new PoloConfirmationRequestMessage
                {
                    StoredInState = StateTestGuid,
                    EnumValue = TestEnum.Value2,
                    OptionalEnumValue = TestEnum.Value1
                }, HandlePoloConfirmationResponse2)

                .YieldSync(ContinuePoloConfirmation);
        }


        [Continuation]
        public void HandlePoloConfirmationResponse1(PoloConfirmationResponseMessage message)
        {
            Console.WriteLine(">> HandlePoloConfirmationResponse1");
            Console.WriteLine(message.ShouldMatchState.Equals(StateTestGuid) ? "Confirmed!" : "Oops! Mismatch!");            
        }


        [Continuation]
        public void HandlePoloConfirmationResponse2(PoloConfirmationResponseMessage message)
        {
            Console.WriteLine(">> HandlePoloConfirmationResponse2");
            Console.WriteLine(message.ShouldMatchState.Equals(StateTestGuid) ? "Confirmed!" : "Oops! Mismatch!");
        }


        private IYieldPoint ContinuePoloConfirmation()
        {
            Console.WriteLine("> ConvergePoloConfirmation (ending flow)");
            return flowProvider.End();
        }


        /**
         * For simple request response patterns, the return type can be used.
         * This will automatically include the correlationId in the response and
         * use the replyTo header of the request if provided.
         */
        [DurableQueue("tapeti.test.durable")]
        public async Task<PoloConfirmationResponseMessage> PoloConfirmation(PoloConfirmationRequestMessage message)
        {
            Console.WriteLine(">> PoloConfirmation (returning confirmation)");
            await Task.Delay(100);

            return new PoloConfirmationResponseMessage
            {
                ShouldMatchState = message.StoredInState,
                EnumValue = message.EnumValue,
                OptionalEnumValue = message.OptionalEnumValue
            };
        }



        [DynamicQueue("custom.prefix")]
        public void Polo(PoloMessage message)
        {
            Console.WriteLine(">> Polo");
        }
    }


    public enum TestEnum
    {
        Value1,
        Value2
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
        [Required]
        public Guid StoredInState { get; set; }

        public TestEnum EnumValue;
        public TestEnum? OptionalEnumValue;
    }


    public class PoloConfirmationResponseMessage
    {
        [Required]
        public Guid ShouldMatchState { get; set; }

        public TestEnum EnumValue;
        public TestEnum? OptionalEnumValue;
    }
}

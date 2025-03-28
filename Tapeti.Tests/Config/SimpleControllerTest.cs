using System.Linq;
using Shouldly;
using Tapeti.Config.Annotations;
using Tapeti.Config;
using Xunit;

namespace Tapeti.Tests.Config
{
    public class SimpleControllerTest : BaseControllerTest
    {
        [Fact]
        public void RegisterController()
        {
            var bindings = GetControllerBindings<TestController>();
            bindings.Count.ShouldBe(2);

            var handleSimpleMessageBinding = bindings.Single(b => b is IControllerMethodBinding cmb &&
                                                                  cmb.Controller == typeof(TestController) &&
                                                                  cmb.Method.Name == "HandleSimpleMessage");
            handleSimpleMessageBinding.QueueType.ShouldBe(QueueType.Dynamic);


            var handleSimpleMessageStaticBinding = bindings.Single(b => b is IControllerMethodBinding cmb &&
                                                                   cmb.Controller == typeof(TestController) &&
                                                                   cmb.Method.Name == "HandleSimpleMessageStatic");
            handleSimpleMessageStaticBinding.QueueType.ShouldBe(QueueType.Dynamic);

        }


        // ReSharper disable all
        #pragma warning disable

        private class TestMessage
        {
        }


        [DynamicQueue]
        private class TestController
        {
            public void HandleSimpleMessage(TestMessage message)
            {
            }

            public static void HandleSimpleMessageStatic(TestMessage message)
            {
            }
        }

        #pragma warning restore
        // ReSharper restore all
    }
}
using System.Linq;
using FluentAssertions;
using Tapeti.Annotations;
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
            bindings.Should().HaveCount(1);

            var handleSimpleMessageBinding = bindings.Single(b => b is IControllerMethodBinding cmb &&
                                                                  cmb.Controller == typeof(TestController) &&
                                                                  cmb.Method.Name == "HandleSimpleMessage");
            handleSimpleMessageBinding.QueueType.Should().Be(QueueType.Dynamic);
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
        }

        #pragma warning restore
        // ReSharper restore all
    }
}
using System;
using Tapeti.Default;
using Xunit;

namespace Tapet.Tests
{
    // ReSharper disable InconsistentNaming
    public class TypeNameRoutingKeyStrategyTests
    {
        private class Example { }

        [Fact]
        public void Singleword()
        {
            AssertRoutingKey("example", typeof(Example));
        }


        private class ExampleMessage { }

        [Fact]
        public void SinglewordMessagePostfix()
        {
            AssertRoutingKey("example", typeof(ExampleMessage));
        }


        private class ExampleMultiWord { }

        [Fact]
        public void Multiword()
        {
            AssertRoutingKey("example.multi.word", typeof(ExampleMultiWord));
        }


        private class ExampleMultiWordMessage { }

        [Fact]
        public void MultiwordMessagePostfix()
        {
            AssertRoutingKey("example.multi.word", typeof(ExampleMultiWordMessage));
        }


        private class ACRTestMessage { }

        [Fact]
        public void Acronym()
        {
            AssertRoutingKey("acr.test", typeof(ACRTestMessage));
        }


        private class ACRTestMIXEDCaseMESSAGE { }

        [Fact]
        public void MixedCasing()
        {
            AssertRoutingKey("acr.test.mixed.case", typeof(ACRTestMIXEDCaseMESSAGE));
        }

        private void AssertRoutingKey(string expected, Type messageType)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));

            Assert.Equal(expected, new TypeNameRoutingKeyStrategy().GetRoutingKey(messageType));
        }
    }
    // ReSharper restore InconsistentNaming
}

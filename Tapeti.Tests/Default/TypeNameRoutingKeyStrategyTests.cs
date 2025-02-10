using System;
using Shouldly;
using Tapeti.Annotations;
using Tapeti.Default;
using Xunit;

namespace Tapeti.Tests.Default
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


        [RoutingKey(Prefix = "prefix.")]
        private class PrefixAttributeTestMessage { }

        [Fact]
        public void Prefix()
        {
            AssertRoutingKey("prefix.prefix.attribute.test", typeof(PrefixAttributeTestMessage));
        }


        [RoutingKey(Postfix = ".postfix")]
        private class PostfixAttributeTestMessage { }

        [Fact]
        public void Postfix()
        {
            AssertRoutingKey("postfix.attribute.test.postfix", typeof(PostfixAttributeTestMessage));
        }



        [RoutingKey(Prefix = "prefix.", Postfix = ".postfix")]
        private class PrefixPostfixAttributeTestMessage { }

        [Fact]
        public void PrefixPostfix()
        {
            AssertRoutingKey("prefix.prefix.postfix.attribute.test.postfix", typeof(PrefixPostfixAttributeTestMessage));
        }


        [RoutingKey(Full = "andnowforsomethingcompletelydifferent", Prefix = "ignore.", Postfix = ".me")]
        private class FullAttributeTestMessage { }

        [Fact]
        public void Full()
        {
            AssertRoutingKey("andnowforsomethingcompletelydifferent", typeof(FullAttributeTestMessage));
        }



        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private static void AssertRoutingKey(string expected, Type messageType)
        {
            var routingKey = new TypeNameRoutingKeyStrategy().GetRoutingKey(messageType);
            routingKey.ShouldBe(expected);
        }
    }
    // ReSharper restore InconsistentNaming
}

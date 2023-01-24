using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Tapeti.Helpers;
using Xunit;

namespace Tapeti.Tests.Helpers
{
    public class ExpressionInvokerTest
    {
        [Fact]
        public void InstanceMethodVoidNoParameters()
        {
            const string methodName = nameof(InvokeTarget.InstanceMethodVoidNoParameters);
            var invoker = InvokerFor(methodName);

            var target = new InvokeTarget();
            invoker.Invoke(target);
            
            target.Verify(methodName);
        }


        [Fact]
        public void InstanceMethodReturnValueNoParameters()
        {
            const string methodName = nameof(InvokeTarget.InstanceMethodReturnValueNoParameters);
            var invoker = InvokerFor(methodName);

            var target = new InvokeTarget();
            var returnValue = invoker.Invoke(target);

            target.Verify(methodName);
            returnValue.Should().Be("Hello world!");
        }


        [Fact]
        public void InstanceMethodVoidParameters()
        {
            const string methodName = nameof(InvokeTarget.InstanceMethodVoidParameters);
            var invoker = InvokerFor(methodName);

            var target = new InvokeTarget();
            invoker.Invoke(target, 42);

            target.Verify(methodName, "42");
        }


        [Fact]
        public void InstanceMethodReturnValueParameters()
        {
            const string methodName = nameof(InvokeTarget.InstanceMethodReturnValueParameters);
            var invoker = InvokerFor(methodName);

            var target = new InvokeTarget();
            var returnValue = invoker.Invoke(target, new byte[] { 42, 69 });

            target.Verify(methodName, "42,69");
            returnValue.Should().Be(true);
        }


        [Fact]
        public void StaticMethodVoidNoParameters()
        {
            InvokeTarget.ResetStatic();

            const string methodName = nameof(InvokeTarget.StaticMethodVoidNoParameters);
            var invoker = InvokerFor(methodName);

            invoker.Invoke(null);

            InvokeTarget.VerifyStatic(methodName);
        }


        [Fact]
        public void StaticMethodReturnValueNoParameters()
        {
            InvokeTarget.ResetStatic();

            const string methodName = nameof(InvokeTarget.StaticMethodReturnValueNoParameters);
            var invoker = InvokerFor(methodName);

            var returnValue = invoker.Invoke(null);

            InvokeTarget.VerifyStatic(methodName);
            returnValue.Should().Be("Hello world!");
        }


        [Fact]
        public void StaticMethodVoidParameters()
        {
            InvokeTarget.ResetStatic();

            const string methodName = nameof(InvokeTarget.StaticMethodVoidParameters);
            var invoker = InvokerFor(methodName);

            invoker.Invoke(null, 42);

            InvokeTarget.VerifyStatic(methodName, "42");
        }


        [Fact]
        public void StaticMethodReturnValueParameters()
        {
            InvokeTarget.ResetStatic();

            const string methodName = nameof(InvokeTarget.StaticMethodReturnValueParameters);
            var invoker = InvokerFor(methodName);

            var returnValue = invoker.Invoke(null, new byte[] { 42, 69 });

            InvokeTarget.VerifyStatic(methodName, "42,69");
            returnValue.Should().Be(true);
        }


        private static ExpressionInvoke InvokerFor(string invokeTargetMethodName)
        {
            var method = typeof(InvokeTarget).GetMethod(invokeTargetMethodName);
            return method!.CreateExpressionInvoke();
        }



        // ReSharper disable ParameterHidesMember
        private class InvokeTarget
        {
            private static string? staticMethodName;
            private static string? staticParameters;

            private string? methodName;
            private string? parameters;


            public void InstanceMethodVoidNoParameters()
            {
                MethodCalled();
            }

            public string InstanceMethodReturnValueNoParameters()
            {
                MethodCalled();
                return "Hello world!";
            }

            public void InstanceMethodVoidParameters(int answer)
            {
                MethodCalled(answer.ToString());
            }

            public bool InstanceMethodReturnValueParameters(IEnumerable<byte> values)
            {
                MethodCalled(string.Join(',', values.Select(v => v.ToString())));
                return true;
            }


            public static void StaticMethodVoidNoParameters()
            {
                StaticMethodCalled();
            }

            public static string StaticMethodReturnValueNoParameters()
            {
                StaticMethodCalled();
                return "Hello world!";
            }

            public static void StaticMethodVoidParameters(int answer)
            {
                StaticMethodCalled(answer.ToString());
            }

            public static bool StaticMethodReturnValueParameters(IEnumerable<byte> values)
            {
                StaticMethodCalled(string.Join(',', values.Select(v => v.ToString())));
                return true;
            }


            private void MethodCalled(string parameters = "", [CallerMemberName]string methodName = "")
            {
                this.methodName.Should().BeNull();
                this.methodName = methodName;
                this.parameters = parameters;

            }


            public static void ResetStatic()
            {
                staticMethodName = null;
                staticParameters = null;
            }


            private static void StaticMethodCalled(string parameters = "", [CallerMemberName] string methodName = "")
            {
                staticMethodName.Should().BeNull();
                staticMethodName = methodName;
                staticParameters = parameters;
            }



            public void Verify(string methodName, string parameters = "")
            {
                this.methodName.Should().Be(methodName);
                this.parameters.Should().Be(parameters);
            }


            public static void VerifyStatic(string methodName, string parameters = "")
            {
                staticMethodName.Should().Be(methodName);
                staticParameters.Should().Be(parameters);
            }
        }
    }
}

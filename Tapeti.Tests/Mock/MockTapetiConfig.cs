using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Tests.Mock
{
    internal class MockTapetiConfig : ITapetiConfig
    {
        private readonly MockTapetiConfigBindings bindings = new();


        public IDependencyResolver DependencyResolver => null!;

        public ITapetiConfigFeatures GetFeatures()
        {
            throw new NotImplementedException();
        }

        public ITapetiConfigMiddleware Middleware => null!;
        public ITapetiConfigBindings Bindings => bindings;



        public void AddMockBinding(MethodInfo methodInfo)
        {
            bindings.Add(methodInfo);
        }
    }


    internal class MockTapetiConfigBindings : ITapetiConfigBindings
    {
        private readonly HashSet<MethodInfo> validMethods = new();


        public IEnumerator<IBinding> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => 0;

        public IBinding this[int index] => throw new NotImplementedException();

        public IControllerMethodBinding ForMethod(Delegate method)
        {
            throw new NotImplementedException();
        }

        public IControllerMethodBinding? ForMethod(MethodInfo method)
        {
            return validMethods.Contains(method) ? new MockControllerMethodBinding(method) : null;
        }


        public void Add(MethodInfo methodInfo)
        {
            validMethods.Add(methodInfo);
        }
    }


    internal class MockControllerMethodBinding : IControllerMethodBinding
    {
        public MockControllerMethodBinding(MethodInfo method)
        {
            Method = method;
        }


        public string? QueueName => null;
        public QueueType? QueueType => null;
        public bool DedicatedChannel => false;

        public ValueTask Apply(IBindingTarget target)
        {
            throw new NotImplementedException();
        }

        public bool Accept(Type messageClass)
        {
            throw new NotImplementedException();
        }

        public ValueTask Invoke(IMessageContext context)
        {
            throw new NotImplementedException();
        }

        public ValueTask Cleanup(IMessageContext context, ConsumeResult consumeResult)
        {
            throw new NotImplementedException();
        }

        public Type Controller => throw new NotImplementedException();
        public MethodInfo Method { get; }
    }
}

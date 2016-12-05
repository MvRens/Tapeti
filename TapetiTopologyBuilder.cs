using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tapeti.Annotations;


namespace Tapeti
{
    public class TopologyConfigurationException : Exception
    {
        public TopologyConfigurationException(string message) : base(message) { }
    }


    public class TapetiTopologyBuilder
    {
        private readonly List<HandlerRegistration> registrations = new List<HandlerRegistration>();


        public ITopology Build()
        {
            throw new NotImplementedException();
        }


        public TapetiTopologyBuilder RegisterController(Type controller)
        {
            var controllerRegistration = GetAttributesRegistration(controller);

            foreach (var method in controller.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object))
                .Select(m => (MethodInfo)m))
            {
            }

            /*
            if (queueAttribute.Dynamic)
            {
                if (!string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Dynamic queue attributes must not have a Name");

                registrations.Value.Add(new ControllerDynamicQueueRegistration(
                    DependencyResolver.Resolve<IControllerFactory>,
                    DependencyResolver.Resolve<IRoutingKeyStrategy>,
                    type, SubscribeExchange));
            }
            else
            {
                if (string.IsNullOrEmpty(queueAttribute.Name))
                    throw new ArgumentException("Non-dynamic queue attribute must have a Name");

                registrations.Value.Add(new ControllerQueueRegistration(
                    DependencyResolver.Resolve<IControllerFactory>,
                    type, SubscribeExchange, queueAttribute.Name));
            }

            (DependencyResolver as IDependencyInjector)?.RegisterController(type);
            */
            return this;
        }


        public TapetiTopologyBuilder RegisterAllControllers(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsDefined(typeof(MessageControllerAttribute))))
                RegisterController(type);

            return this;
        }


        public TapetiTopologyBuilder RegisterAllControllers()
        {
            return RegisterAllControllers(Assembly.GetCallingAssembly());
        }


        protected HandlerRegistration GetAttributesRegistration(MemberInfo member)
        {
            var registration = new HandlerRegistration();

            var dynamicQueueAttribute = member.GetCustomAttribute<DynamicQueueAttribute>();
            var staticQueueAttribute = member.GetCustomAttribute<StaticQueueAttribute>();

            if (dynamicQueueAttribute != null && staticQueueAttribute != null)
                throw new TopologyConfigurationException($"Cannot combine static and dynamic queue attributes on {member.Name}");

            if (dynamicQueueAttribute != null)
                registration.Dynamic = true;
            else if (staticQueueAttribute != null)
            {
                registration.Dynamic = false;
                registration.QueueName = staticQueueAttribute.Name;
            }

            return registration;
        }


        protected class HandlerRegistration
        {
            public bool? Dynamic { get; set; }
            public string QueueName { get; set; }
        }


        protected class Topology : ITopology
        {
            private readonly List<Queue> queues = new List<Queue>();


            public void Add(Queue queue)
            {
                queues.Add(queue);
            }

            public IEnumerable<IQueue> Queues()
            {
                return queues;
            }
        }


        protected class Queue : IQueue
        {
            private readonly List<Binding> bindings = new List<Binding>();


            public void Add(Binding binding)
            {
                bindings.Add(binding);
            }

            public IEnumerable<IBinding> Bindings()
            {
                return bindings;
            }
        }


        protected class Binding : IBinding
        {
        }
    }
}

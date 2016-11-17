using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Tapeti.Annotations;

namespace Tapeti.Registration
{
    using MessageHandlerAction = Func<object, Task>;

    public abstract class AbstractControllerRegistration : IMessageHandlerRegistration
    {
        private readonly IControllerFactory controllerFactory;
        private readonly Type controllerType;
        private readonly Dictionary<Type, List<MessageHandlerAction>> messageHandlers = new Dictionary<Type, List<MessageHandlerAction>>();


        protected AbstractControllerRegistration(IControllerFactory controllerFactory, Type controllerType)
        {
            this.controllerFactory = controllerFactory;
            this.controllerType = controllerType;

            // ReSharper disable once VirtualMemberCallInConstructor - I know. What do you think this is, C++?
            GetMessageHandlers((type, handler) =>
            {
                if (!messageHandlers.ContainsKey(type))
                    messageHandlers.Add(type, new List<MessageHandlerAction> { handler });
                else
                    messageHandlers[type].Add(handler);
            });
        }


        protected virtual void GetMessageHandlers(Action<Type, MessageHandlerAction> add)
        {
            foreach (var method in GetType().GetMembers()
                .Where(m => m.MemberType == MemberTypes.Method && m.IsDefined(typeof(MessageHandlerAttribute), true))
                .Select(m => (MethodInfo)m))
            {
                var parameters = method.GetParameters();

                if (parameters.Length != 1 || !parameters[0].ParameterType.IsClass)
                    throw new ArgumentException($"Method {0} does not have a single object parameter", method.Name);

                var messageType = parameters[0].ParameterType;

                if (method.ReturnType == typeof(void))
                    add(messageType, CreateSyncMessageHandler(method));
                else if (method.ReturnType == typeof(Task))
                    add(messageType, CreateAsyncMessageHandler(method));
                else
                    throw new ArgumentException($"Method {0} needs to return void or a Task", method.Name);
            }
        }


        protected IEnumerable<Type> GetMessageTypes()
        {
            return messageHandlers.Keys;
        }


        public abstract void ApplyTopology(IModel channel);


        public bool Accept(object message)
        {
            return messageHandlers.ContainsKey(message.GetType());
        }


        public Task Visit(object message)
        {
            var registeredHandlers = messageHandlers[message.GetType()];
            if (registeredHandlers != null)
                return Task.WhenAll(registeredHandlers.Select(messageHandler => messageHandler(message)));

            return Task.CompletedTask;
        }


        protected virtual MessageHandlerAction CreateSyncMessageHandler(MethodInfo method)
        {
            return message =>
            {
                var controller = controllerFactory.CreateController(controllerType);
                method.Invoke(controller, new[] { message });

                return Task.CompletedTask;
            };
        }


        protected virtual MessageHandlerAction CreateAsyncMessageHandler(MethodInfo method)
        {
            return message =>
            {
                var controller = controllerFactory.CreateController(controllerType);
                return (Task)method.Invoke(controller, new[] { message });
            };
        }
    }
}

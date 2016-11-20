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

    public struct MessageHandler
    {
        public MessageHandlerAction Action;
        public string Exchange;
        public string RoutingKey;
    }


    public abstract class AbstractControllerRegistration : IQueueRegistration
    {
        private readonly Func<IControllerFactory> controllerFactoryFactory;
        private readonly Type controllerType;
        private readonly string defaultExchange;
        private readonly Dictionary<Type, List<MessageHandler>> messageHandlers = new Dictionary<Type, List<MessageHandler>>();


        protected AbstractControllerRegistration(Func<IControllerFactory> controllerFactoryFactory, Type controllerType, string defaultExchange)
        {
            this.controllerFactoryFactory = controllerFactoryFactory;
            this.controllerType = controllerType;
            this.defaultExchange = defaultExchange;

            // ReSharper disable once VirtualMemberCallInConstructor - I know. What do you think this is, C++?
            GetMessageHandlers(controllerType, (type, handler) =>
            {
                if (!messageHandlers.ContainsKey(type))
                    messageHandlers.Add(type, new List<MessageHandler> { handler });
                else
                    messageHandlers[type].Add(handler);
            });
        }


        protected virtual void GetMessageHandlers(Type type, Action<Type, MessageHandler> add)
        {
            foreach (var method in type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.MemberType == MemberTypes.Method && m.DeclaringType != typeof(object))
                .Select(m => (MethodInfo)m))
            {
                Type messageType;
                var messageHandler = GetMessageHandler(method, out messageType);

                add(messageType, messageHandler);
            }
        }


        protected virtual MessageHandler GetMessageHandler(MethodInfo method, out Type messageType)
        {
            var parameters = method.GetParameters();

            if (parameters.Length != 1 || !parameters[0].ParameterType.IsClass)
                throw new ArgumentException($"Method {method.Name} does not have a single object parameter");

            messageType = parameters[0].ParameterType;
            var messageHandler = new MessageHandler();

            if (method.ReturnType == typeof(void))
                messageHandler.Action = CreateSyncMessageHandler(method);
            else if (method.ReturnType == typeof(Task))
                messageHandler.Action = CreateAsyncMessageHandler(method);
            else
                throw new ArgumentException($"Method {method.Name} needs to return void or a Task");

            var exchangeAttribute = method.GetCustomAttribute<ExchangeAttribute>() ?? method.DeclaringType.GetCustomAttribute<ExchangeAttribute>();
            messageHandler.Exchange = exchangeAttribute?.Name;

            return messageHandler;
        }


        protected IEnumerable<Type> GetMessageTypes()
        {
            return messageHandlers.Keys;
        }


        protected IEnumerable<string> GetMessageExchanges(Type type)
        {
            var exchanges = messageHandlers[type]
                .Where(h => h.Exchange != null)
                .Select(h => h.Exchange)
                .Distinct(StringComparer.InvariantCulture)
                .ToArray();

            return exchanges.Length > 0 ? exchanges : new[] { defaultExchange };
        }


        public abstract string BindQueue(IModel channel);


        public bool Accept(object message)
        {
            return messageHandlers.ContainsKey(message.GetType());
        }


        public Task Visit(object message)
        {
            var registeredHandlers = messageHandlers[message.GetType()];
            if (registeredHandlers != null)
                return Task.WhenAll(registeredHandlers.Select(messageHandler => messageHandler.Action(message)));

            return Task.CompletedTask;
        }


        protected virtual MessageHandlerAction CreateSyncMessageHandler(MethodInfo method)
        {
            return message =>
            {
                var controller = controllerFactoryFactory().CreateController(controllerType);
                method.Invoke(controller, new[] { message });

                return Task.CompletedTask;
            };
        }


        protected virtual MessageHandlerAction CreateAsyncMessageHandler(MethodInfo method)
        {
            return message =>
            {
                var controller = controllerFactoryFactory().CreateController(controllerType);
                return (Task)method.Invoke(controller, new[] { message });
            };
        }
    }
}

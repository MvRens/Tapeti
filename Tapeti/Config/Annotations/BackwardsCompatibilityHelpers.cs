using System;
using System.Reflection;

#pragma warning disable CS0618 // Obsolete
#pragma warning disable CS1591 // Missing documentation


namespace Tapeti.Config.Annotations
{
    /// <summary>
    /// Provides extensions methods to support moved (marked obsolete) attributes from Tapeti.Annotations.
    /// </summary>
    public static class BackwardsCompatibilityHelpers
    {
        public static DurableQueueAttribute? GetDurableQueueAttribute(this MemberInfo member)
        {
            return member.GetCustomAttribute<DurableQueueAttribute>() ?? Upgrade(member.GetCustomAttribute<Tapeti.Annotations.DurableQueueAttribute>());
        }

        public static DynamicQueueAttribute? GetDynamicQueueAttribute(this MemberInfo member)
        {
            return member.GetCustomAttribute<DynamicQueueAttribute>() ?? Upgrade(member.GetCustomAttribute<Tapeti.Annotations.DynamicQueueAttribute>());
        }

        public static QueueArgumentsAttribute? GetQueueArgumentsAttribute(this MemberInfo member)
        {
            return member.GetCustomAttribute<QueueArgumentsAttribute>() ?? Upgrade(member.GetCustomAttribute<Tapeti.Annotations.QueueArgumentsAttribute>());
        }

        public static ResponseHandlerAttribute? GetResponseHandlerAttribute(this MemberInfo member)
        {
            return member.GetCustomAttribute<ResponseHandlerAttribute>() ?? Upgrade(member.GetCustomAttribute<Tapeti.Annotations.ResponseHandlerAttribute>());
        }


        public static bool HasMessageControllerAttribute(this MemberInfo member)
        {
            return member.IsDefined(typeof(MessageControllerAttribute)) || member.IsDefined(typeof(Tapeti.Annotations.MessageControllerAttribute));
        }


        private static DurableQueueAttribute? Upgrade(Tapeti.Annotations.DurableQueueAttribute? attribute)
        {
            return attribute == null ? null : new DurableQueueAttribute(attribute.Name);
        }

        private static DynamicQueueAttribute? Upgrade(Tapeti.Annotations.DynamicQueueAttribute? attribute)
        {
            return attribute == null ? null : new DynamicQueueAttribute(attribute.Prefix);
        }

        private static QueueArgumentsAttribute? Upgrade(Tapeti.Annotations.QueueArgumentsAttribute? attribute)
        {
            return attribute == null
                ? null
                : new QueueArgumentsAttribute(attribute.CustomArguments)
                {
                    MaxLength = attribute.MaxLength,
                    MaxLengthBytes = attribute.MaxLengthBytes,
                    Overflow = attribute.Overflow switch
                    {
                        Tapeti.Annotations.RabbitMQOverflow.NotSpecified => RabbitMQOverflow.NotSpecified,
                        Tapeti.Annotations.RabbitMQOverflow.DropHead => RabbitMQOverflow.DropHead,
                        Tapeti.Annotations.RabbitMQOverflow.RejectPublish => RabbitMQOverflow.RejectPublish,
                        Tapeti.Annotations.RabbitMQOverflow.RejectPublishDeadletter => RabbitMQOverflow.RejectPublishDeadletter,
                        _ => throw new ArgumentOutOfRangeException(nameof(attribute.Overflow))
                    },
                    MessageTTL = attribute.MessageTTL
                };
        }

        private static ResponseHandlerAttribute? Upgrade(Tapeti.Annotations.ResponseHandlerAttribute? attribute)
        {
            return attribute == null ? null : new ResponseHandlerAttribute();
        }
    }
}

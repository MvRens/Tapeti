﻿using System;
using System.Threading.Tasks;

namespace Tapeti.Config
{
    /// <summary>
    /// Determines the type of queue the binding registers
    /// </summary>
    public enum QueueType
    {
        /// <summary>
        /// The consumed queue is durable
        /// </summary>
        Durable,

        /// <summary>
        /// The consumed queue is dynamic
        /// </summary>
        Dynamic
    }


    /// <summary>
    /// Represents a registered binding to handle incoming messages.
    /// </summary>
    public interface IBinding
    {
        /// <summary>
        /// The name of the queue the binding is consuming. May change after a reconnect for dynamic queues.
        /// </summary>
        string QueueName { get; }


        /// <summary>
        /// Determines the type of queue the binding registers
        /// </summary>
        QueueType QueueType { get; }


        /// <summary>
        /// Called after a connection is established to set up the binding.
        /// </summary>
        /// <param name="target"></param>
        Task Apply(IBindingTarget target);


        /// <summary>
        /// Determines if the message as specified by the message class can be handled by this binding.
        /// </summary>
        /// <param name="messageClass"></param>
        bool Accept(Type messageClass);


        /// <summary>
        /// Invokes the handler for the message as specified by the context.
        /// </summary>
        /// <param name="context"></param>
        Task Invoke(IMessageContext context);


        /// <summary>
        /// Called after the handler is invoked and any exception handling has been done.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="consumeResult"></param>
        /// <returns></returns>
        Task Cleanup(IMessageContext context, ConsumeResult consumeResult);
    }



    /// <summary>
    /// Allows the binding to specify to which queue it should bind to and how.
    /// At most one of these methods can be called, calling a second method will result in an exception.
    /// </summary>
    public interface IBindingTarget
    {
        /// <summary>
        /// Binds the messageClass to the specified durable queue.
        /// </summary>
        /// <param name="messageClass">The message class to be bound to the queue</param>
        /// <param name="queueName">The name of the durable queue</param>
        Task BindDurable(Type messageClass, string queueName);

        /// <summary>
        /// Binds the messageClass to a dynamic auto-delete queue.
        /// </summary>
        /// <remarks>
        /// Dynamic bindings for different messageClasses will be bundled into a single dynamic queue.
        /// Specifying a different queuePrefix is a way to force bindings into separate queues.
        /// </remarks>
        /// <param name="messageClass">The message class to be bound to the queue</param>
        /// <param name="queuePrefix">An optional prefix for the dynamic queue's name. If not provided, RabbitMQ's default logic will be used to create an amq.gen queue.</param>
        /// <returns>The generated name of the dynamic queue</returns>
        Task<string> BindDynamic(Type messageClass, string queuePrefix = null);

        /// <summary>
        /// Declares a durable queue but does not add a binding for a messageClass' routing key.
        /// Used for direct-to-queue messages.
        /// </summary>
        /// <param name="queueName">The name of the durable queue</param>
        Task BindDurableDirect(string queueName);

        /// <summary>
        /// Declares a dynamic queue but does not add a binding for a messageClass' routing key.
        /// Used for direct-to-queue messages. The messageClass is used to ensure each queue only handles unique message types.
        /// </summary>
        /// <param name="messageClass">The message class which will be handled on the queue. It is not actually bound to the queue.</param>
        /// <param name="queuePrefix">An optional prefix for the dynamic queue's name. If not provided, RabbitMQ's default logic will be used to create an amq.gen queue.</param>
        /// <returns>The generated name of the dynamic queue</returns>
        Task<string> BindDynamicDirect(Type messageClass = null, string queuePrefix = null);

        /// <summary>
        /// Declares a dynamic queue but does not add a binding for a messageClass' routing key.
        /// Used for direct-to-queue messages. Guarantees a unique queue.
        /// </summary>
        /// <param name="queuePrefix">An optional prefix for the dynamic queue's name. If not provided, RabbitMQ's default logic will be used to create an amq.gen queue.</param>
        /// <returns>The generated name of the dynamic queue</returns>
        Task<string> BindDynamicDirect(string queuePrefix = null);

        /// <summary>
        /// Marks the specified durable queue as having an obsolete binding. If after all bindings have subscribed, the queue only contains obsolete
        /// bindings and is empty, it will be removed.
        /// </summary>
        /// <param name="queueName">The name of the durable queue</param>
        Task BindDurableObsolete(string queueName);
    }
}

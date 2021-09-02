using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Config
{
    /// <summary>
    /// Injects a value for a controller method parameter.
    /// </summary>
    /// <param name="context"></param>
    public delegate object ValueFactory(IMessageContext context);


    /// <summary>
    /// Handles the return value of a controller method.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="value"></param>
    public delegate Task ResultHandler(IMessageContext context, object value);


    /// <summary>
    /// Determines how the binding target is configured.
    /// </summary>
    public enum BindingTargetMode
    {
        /// <summary>
        /// Bind to a queue using the message's routing key
        /// </summary>
        Default,

        /// <summary>
        /// Bind to a queue without registering the message's routing key
        /// </summary>
        Direct
    }


    /// <summary>
    /// Provides information about the controller and method being registered.
    /// </summary>
    public interface IControllerBindingContext
    {
        /// <summary>
        /// The message class for this method. Can be null if not yet set by the default MessageBinding or other middleware.
        /// If required, call next first to ensure it is available.
        /// </summary>
        Type MessageClass { get; }

        /// <summary>
        /// Determines if SetMessageClass has already been called.
        /// </summary>
        bool HasMessageClass { get; }

        /// <summary>
        /// The controller class for this binding.
        /// </summary>
        Type Controller { get; }

        /// <summary>
        /// The method for this binding.
        /// </summary>
        MethodInfo Method { get; }

        /// <summary>
        /// The list of parameters passed to the method.
        /// </summary>
        IReadOnlyList<IBindingParameter> Parameters { get; }

        /// <summary>
        /// The return type of the method.
        /// </summary>
        IBindingResult Result { get; }


        /// <summary>
        /// Sets the message class for this method. Can only be called once, which is normally done by the default MessageBinding.
        /// </summary>
        /// <param name="messageClass"></param>
        void SetMessageClass(Type messageClass);


        /// <summary>
        /// Determines how the binding target is configured. Can only be called once. Defaults to 'Default'.
        /// </summary>
        /// <param name="mode"></param>
        void SetBindingTargetMode(BindingTargetMode mode);


        /// <summary>
        /// Add middleware specific to this method.
        /// </summary>
        /// <param name="handler"></param>
        void Use(IControllerMiddlewareBase handler);
    }


    /// <summary>
    /// Information about a method parameter and how it gets it's value.
    /// </summary>
    public interface IBindingParameter
    {
        /// <summary>
        /// Reference to the reflection info for this parameter.
        /// </summary>
        ParameterInfo Info { get; }

        /// <summary>
        /// Determines if a binding has been set.
        /// </summary>
        bool HasBinding { get; }

        /// <summary>
        /// Sets the binding for this parameter. Can only be called once.
        /// </summary>
        /// <param name="valueFactory"></param>
        void SetBinding(ValueFactory valueFactory);
    }


    /// <summary>
    /// Information about the return type of a method.
    /// </summary>
    public interface IBindingResult
    {
        /// <summary>
        /// Reference to the reflection info for this return value.
        /// </summary>
        ParameterInfo Info { get; }

        /// <summary>
        /// Determines if a handler has been set.
        /// </summary>
        bool HasHandler { get; }

        /// <summary>
        /// Sets the handler for this return type. Can only be called once.
        /// </summary>
        /// <param name="resultHandler"></param>
        void SetHandler(ResultHandler resultHandler);
    }
}   

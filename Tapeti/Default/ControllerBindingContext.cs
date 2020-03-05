using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tapeti.Config;

namespace Tapeti.Default
{
    internal class ControllerBindingContext : IControllerBindingContext
    {
        private BindingTargetMode? bindingTargetMode;
        private readonly List<IControllerMiddlewareBase> middleware = new List<IControllerMiddlewareBase>();
        private readonly List<ControllerBindingParameter> parameters;
        private readonly ControllerBindingResult result;

        /// <summary>
        /// Determines how the binding target is configured.
        /// </summary>
        public BindingTargetMode BindingTargetMode => bindingTargetMode ?? BindingTargetMode.Default;


        /// <summary>
        /// Provides access to the registered middleware for this method.
        /// </summary>
        public IReadOnlyList<IControllerMiddlewareBase> Middleware => middleware;


        /// <inheritdoc />
        public Type MessageClass { get; set; }

        /// <inheritdoc />
        public bool HasMessageClass => MessageClass != null;

        /// <inheritdoc />
        public Type Controller { get; set; }

        /// <inheritdoc />
        public MethodInfo Method { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<IBindingParameter> Parameters => parameters;

        /// <inheritdoc />
        public IBindingResult Result => result;


        public ControllerBindingContext(IEnumerable<ParameterInfo> parameters, ParameterInfo result)
        {
            this.parameters = parameters.Select(parameter => new ControllerBindingParameter(parameter)).ToList();

            this.result = new ControllerBindingResult(result);
        }


        /// <inheritdoc />
        public void SetMessageClass(Type messageClass)
        {
            if (HasMessageClass)
                throw new InvalidOperationException("SetMessageClass can only be called once");

            MessageClass = messageClass;
        }


        /// <inheritdoc />
        public void SetBindingTargetMode(BindingTargetMode mode)
        {
            if (bindingTargetMode.HasValue)
                throw new InvalidOperationException("SetBindingTargetMode can only be called once");

            bindingTargetMode = mode;
        }


        /// <inheritdoc />
        public void Use(IControllerMiddlewareBase handler)
        {
            middleware.Add(handler);
        }


        /// <summary>
        /// Returns the configured bindings for the parameters.
        /// </summary>
        public IEnumerable<ValueFactory> GetParameterHandlers()
        {
            return parameters.Select(p => p.Binding);
        }


        /// <summary>
        /// Returns the configured result handler.
        /// </summary>
        /// <returns></returns>
        public ResultHandler GetResultHandler()
        {
            return result.Handler;
        }
    }


    /// <inheritdoc />
    /// <summary>
    /// Default implementation for IBindingParameter
    /// </summary>
    public class ControllerBindingParameter : IBindingParameter
    {
        /// <summary>
        /// Provides access to the configured binding.
        /// </summary>
        public ValueFactory Binding { get; set; }


        /// <inheritdoc />
        public ParameterInfo Info { get; }

        /// <inheritdoc />
        public bool HasBinding => Binding != null;


        /// <inheritdoc />
        public ControllerBindingParameter(ParameterInfo info)
        {
            Info = info;
        }


        /// <inheritdoc />
        public void SetBinding(ValueFactory valueFactory)
        {
            if (Binding != null)
                throw new InvalidOperationException("SetBinding can only be called once");

            Binding = valueFactory;
        }
    }


    /// <inheritdoc />
    /// <summary>
    /// Default implementation for IBindingResult
    /// </summary>
    public class ControllerBindingResult : IBindingResult
    {
        /// <summary>
        /// Provides access to the configured handler.
        /// </summary>
        public ResultHandler Handler { get; set; }


        /// <inheritdoc />
        public ParameterInfo Info { get; }

        /// <inheritdoc />
        public bool HasHandler => Handler != null;


        /// <inheritdoc />
        public ControllerBindingResult(ParameterInfo info)
        {
            Info = info;
        }


        /// <inheritdoc />
        public void SetHandler(ResultHandler resultHandler)
        {
            if (Handler != null)
                throw new InvalidOperationException("SetHandler can only be called once");

            Handler = resultHandler;
        }
    }
}

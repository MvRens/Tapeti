using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    /// <summary>
    /// Default implementation for IFlowStarter.
    /// </summary>
    internal class FlowStarter : IFlowStarter
    {
        private readonly ITapetiConfig config;


        /// <summary>
        /// </summary>
        public FlowStarter(ITapetiConfig config)
        {
            this.config = config;
        }


        /// <inheritdoc />
        public async Task Start<TController>(Expression<Func<TController, Func<IYieldPoint>>> methodSelector) where TController : class
        {
            await CallControllerMethod<TController>(GetExpressionMethod(methodSelector), value => Task.FromResult((IYieldPoint)value), Array.Empty<object?>()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task Start<TController>(Expression<Func<TController, Func<Task<IYieldPoint>>>> methodSelector) where TController : class
        {
            await CallControllerMethod<TController>(GetExpressionMethod(methodSelector), value => (Task<IYieldPoint>)value, Array.Empty<object?>()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task Start<TController, TParameter>(Expression<Func<TController, Func<TParameter, IYieldPoint>>> methodSelector, TParameter parameter) where TController : class
        {
            await CallControllerMethod<TController>(GetExpressionMethod(methodSelector), value => Task.FromResult((IYieldPoint)value), new object?[] {parameter}).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task Start<TController, TParameter>(Expression<Func<TController, Func<TParameter, Task<IYieldPoint>>>> methodSelector, TParameter parameter) where TController : class
        {
            await CallControllerMethod<TController>(GetExpressionMethod(methodSelector), value => (Task<IYieldPoint>)value, new object?[] {parameter}).ConfigureAwait(false);
        }


        private async Task CallControllerMethod<TController>(MethodInfo method, Func<object, Task<IYieldPoint>> getYieldPointResult, object?[] parameters) where TController : class
        {
            var controller = config.DependencyResolver.Resolve<TController>();
            var result = method.Invoke(controller, parameters);
            if (result == null)
                throw new InvalidOperationException($"Method {method.Name} must return an IYieldPoint or Task<IYieldPoint>, got null");

            var yieldPoint = await getYieldPointResult(result).ConfigureAwait(false);

            var context = new FlowHandlerContext(config, controller, method);

            var flowHandler = config.DependencyResolver.Resolve<IFlowHandler>();
            await flowHandler.Execute(context, yieldPoint).ConfigureAwait(false);
        }


        private static MethodInfo GetExpressionMethod<TController, TResult>(Expression<Func<TController, Func<TResult>>> methodSelector)
        {
            var callExpression = (methodSelector.Body as UnaryExpression)?.Operand as MethodCallExpression;
            var targetMethodExpression = callExpression?.Object as ConstantExpression;

            var method = targetMethodExpression?.Value as MethodInfo;
            if (method == null)
                throw new ArgumentException("Unable to determine the starting method", nameof(methodSelector));

            return method;
        }


        private static MethodInfo GetExpressionMethod<TController, TResult, TParameter>(Expression<Func<TController, Func<TParameter, TResult>>> methodSelector)
        {
            var callExpression = (methodSelector.Body as UnaryExpression)?.Operand as MethodCallExpression;
            var targetMethodExpression = callExpression?.Object as ConstantExpression;

            var method = targetMethodExpression?.Value as MethodInfo;
            if (method == null)
                throw new ArgumentException("Unable to determine the starting method", nameof(methodSelector));

            return method;
        }
    }
}

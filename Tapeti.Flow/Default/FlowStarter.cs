using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Default;

namespace Tapeti.Flow.Default
{
    public class FlowStarter : IFlowStarter
    {
        private readonly IConfig config;


        public FlowStarter(IConfig config)
        {
            this.config = config;
        }


        public Task Start<TController>(Expression<Func<TController, Func<IYieldPoint>>> methodSelector) where TController : class
        {
            return CallControllerMethod<TController>(GetExpressionMethod(methodSelector), value => Task.FromResult((IYieldPoint)value));
        }


        public Task Start<TController>(Expression<Func<TController, Func<Task<IYieldPoint>>>> methodSelector) where TController : class
        {
            return CallControllerMethod<TController>(GetExpressionMethod(methodSelector), value => (Task<IYieldPoint>)value);
        }


        private async Task CallControllerMethod<TController>(MethodInfo method, Func<object, Task<IYieldPoint>> getYieldPointResult) where TController : class
        {
            var controller = config.DependencyResolver.Resolve<TController>();
            var yieldPoint = await getYieldPointResult(method.Invoke(controller, new object[] {}));

            var context = new MessageContext
            {
                DependencyResolver = config.DependencyResolver,
                Controller = controller
            };

            var flowHandler = config.DependencyResolver.Resolve<IFlowHandler>();
            await flowHandler.Execute(context, yieldPoint);
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
    }
}

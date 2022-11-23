using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

// Note: I also tried a version which accepts a ValueFactory[] to reduce the object array allocations,
// but the performance benefits were negligable and it still allocated more memory than expected.
//
// Reflection.Emit is another option which I've dabbled with, but that's too much of a risk for me to
// attempt at the moment and there's probably other code which could benefit from optimization more.

namespace Tapeti.Helpers
{
    /// <summary>
    /// The precompiled version of MethodInfo.Invoke.
    /// </summary>
    /// <param name="target">The instance on which the method should be called.</param>
    /// <param name="args">The arguments passed to the method.</param>
    public delegate object ExpressionInvoke(object? target, params object?[] args);


    /// <summary>
    /// Provides a way to create a precompiled version of MethodInfo.Invoke with decreased overhead.
    /// </summary>
    public static class ExpressionInvokeExtensions
    {
        /// <summary>
        /// Creates a precompiled version of MethodInfo.Invoke with decreased overhead.
        /// </summary>
        public static ExpressionInvoke CreateExpressionInvoke(this MethodInfo method)
        {
            if (method.DeclaringType == null)
                throw new ArgumentException("Method must have a declaring type");

            var argsParameter = Expression.Parameter(typeof(object[]), "args");
            var parameters = method.GetParameters().Select(
                (p, i) =>
                {
                    var argsIndexExpression = Expression.Constant(i, typeof(int));
                    var argExpression = Expression.ArrayIndex(argsParameter, argsIndexExpression);

                    return Expression.Convert(argExpression, p.ParameterType) as Expression;
                })
                .ToArray();


            var target = Expression.Parameter(typeof(object), "target");
            var castTarget = Expression.Convert(target, method.DeclaringType);
            var invoke = Expression.Call(castTarget, method, parameters);

            Expression<ExpressionInvoke> lambda;

            if (method.ReturnType != typeof(void))
            {
                var result = Expression.Convert(invoke, typeof(object));
                lambda = Expression.Lambda<ExpressionInvoke>(result, target, argsParameter);
            }
            else
            {
                var nullResult = Expression.Constant(null, typeof(object));
                var body = Expression.Block(invoke, nullResult);
                lambda = Expression.Lambda<ExpressionInvoke>(body, target, argsParameter);
            }

            return lambda.Compile();
        }
    }
}
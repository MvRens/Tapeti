using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Flow
{
    public interface IFlowProvider
    {
        IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler);

        // One does not simply overload methods with Task vs non-Task Funcs. "Ambiguous call".
        // Apparantly this is because a return type of a method is not part of its signature,
        // according to: http://stackoverflow.com/questions/18715979/ambiguity-with-action-and-func-parameter
        IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler);

        IFlowParallelRequestBuilder YieldWithParallelRequest();

        IYieldPoint EndWithResponse<TResponse>(TResponse message);
        IYieldPoint End();
    }

    /// <summary>
    /// Allows starting a flow outside of a message handler.
    /// </summary>
    public interface IFlowStarter
    {
        Task Start<TController>(Expression<Func<TController, Func<IYieldPoint>>> methodSelector) where TController : class;
        Task Start<TController>(Expression<Func<TController, Func<Task<IYieldPoint>>>> methodSelector) where TController : class;
        Task Start<TController, TParameter>(Expression<Func<TController, Func<TParameter, IYieldPoint>>> methodSelector, TParameter parameter) where TController : class;
        Task Start<TController, TParameter>(Expression<Func<TController, Func<TParameter, Task<IYieldPoint>>>> methodSelector, TParameter parameter) where TController : class;
    }

    /// <summary>
    /// Internal interface. Do not call directly.
    /// </summary>
    public interface IFlowHandler
    {
        Task Execute(IMessageContext context, IYieldPoint yieldPoint);
    }

    public interface IFlowParallelRequestBuilder
    {
        IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler);
        IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler);

        IYieldPoint Yield(Func<Task<IYieldPoint>> continuation);
        IYieldPoint YieldSync(Func<IYieldPoint> continuation);
    }

    public interface IYieldPoint
    {
    }
}

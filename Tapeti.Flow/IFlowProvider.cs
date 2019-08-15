using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Flow
{
    /// <summary>
    /// Provides methods to build an IYieldPoint to indicate if and how Flow should continue.
    /// </summary>
    public interface IFlowProvider
    {
        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// The request message must be marked with the [Request] attribute, and the
        /// Response type must match. Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler);


        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// The request message must be marked with the [Request] attribute, and the
        /// Response type must match. Used for synchronous response handlers.
        /// </summary>
        /// <remarks>
        /// The reason why this requires the extra 'Sync' in the name: one does not simply overload methods
        /// with Task vs non-Task Funcs. "Ambiguous call". Apparantly this is because a return type
        /// of a method is not part of its signature,according to:
        /// http://stackoverflow.com/questions/18715979/ambiguity-with-action-and-func-parameter
        /// </remarks>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <returns></returns>
        IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler);


        /// <summary>
        /// Create a request builder to publish one or more requests messages. Call Yield on the resulting builder
        /// to acquire an IYieldPoint.
        /// </summary>
        IFlowParallelRequestBuilder YieldWithParallelRequest();


        /// <summary>
        /// End the flow by publishing the specified response message. Only allowed, and required, when the
        /// current flow was started by a message handler for a Request message.
        /// </summary>
        /// <param name="message"></param>
        /// <typeparam name="TResponse"></typeparam>
        IYieldPoint EndWithResponse<TResponse>(TResponse message);


        /// <summary>
        /// End the flow and dispose any state.
        /// </summary>
        IYieldPoint End();
    }


    /// <summary>
    /// Allows starting a flow outside of a message handler.
    /// </summary>
    public interface IFlowStarter
    {
        /// <summary>
        /// Starts a new flow.
        /// </summary>
        /// <param name="methodSelector"></param>
        Task Start<TController>(Expression<Func<TController, Func<IYieldPoint>>> methodSelector) where TController : class;

        /// <summary>
        /// Starts a new flow.
        /// </summary>
        /// <param name="methodSelector"></param>
        Task Start<TController>(Expression<Func<TController, Func<Task<IYieldPoint>>>> methodSelector) where TController : class;

        /// <summary>
        /// Starts a new flow and passes the parameter to the method.
        /// </summary>
        /// <param name="methodSelector"></param>
        /// <param name="parameter"></param>
        Task Start<TController, TParameter>(Expression<Func<TController, Func<TParameter, IYieldPoint>>> methodSelector, TParameter parameter) where TController : class;

        /// <summary>
        /// Starts a new flow and passes the parameter to the method.
        /// </summary>
        /// <param name="methodSelector"></param>
        /// <param name="parameter"></param>
        Task Start<TController, TParameter>(Expression<Func<TController, Func<TParameter, Task<IYieldPoint>>>> methodSelector, TParameter parameter) where TController : class;
    }


    /// <summary>
    /// Internal interface. Do not call directly.
    /// </summary>
    public interface IFlowHandler
    {
        /// <summary>
        /// Executes the YieldPoint for the given message context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="yieldPoint"></param>
        Task Execute(IFlowHandlerContext context, IYieldPoint yieldPoint);
    }


    /// <summary>
    /// Builder to publish one or more request messages and continuing the flow when the responses arrive.
    /// </summary>
    public interface IFlowParallelRequestBuilder
    {
        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// Note that the response handler can not influence the flow as it does not return a YieldPoint.
        /// It can instead store state in the controller for the continuation passed to the Yield method.
        /// Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler);

        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// Note that the response handler can not influence the flow as it does not return a YieldPoint.
        /// It can instead store state in the controller for the continuation passed to the Yield method.
        /// Used for synchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler);

        /// <summary>
        /// Constructs an IYieldPoint to continue the flow when responses arrive.
        /// The continuation method is called when all responses have arrived.
        /// Response handlers and the continuation method are guaranteed thread-safe access to the
        /// controller and can store state.
        /// Used for asynchronous continuation methods.
        /// </summary>
        /// <param name="continuation"></param>
        IYieldPoint Yield(Func<Task<IYieldPoint>> continuation);

        /// <summary>
        /// Constructs an IYieldPoint to continue the flow when responses arrive.
        /// The continuation method is called when all responses have arrived.
        /// Response handlers and the continuation method are guaranteed thread-safe access to the
        /// controller and can store state.
        /// Used for synchronous continuation methods.
        /// </summary>
        /// <param name="continuation"></param>
        IYieldPoint YieldSync(Func<IYieldPoint> continuation);
    }


    /// <summary>
    /// Defines if and how the Flow should continue. Construct using any of the IFlowProvider methods.
    /// </summary>
    public interface IYieldPoint
    {
    }
}

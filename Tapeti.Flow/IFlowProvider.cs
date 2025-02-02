﻿using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

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
        IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task<IYieldPoint>> responseHandler) where TRequest : class where TResponse : class;


        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// The request message must be marked with the [Request] attribute, and the
        /// Response type must match. Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        IYieldPoint YieldWithRequest<TRequest, TResponse>(TRequest message, Func<TResponse, ValueTask<IYieldPoint>> responseHandler) where TRequest : class where TResponse : class;


        /// <summary>
        /// Publish a request message directly to a queue and continue the flow when the response arrives. 
        /// The exchange and routing key are not used.
        /// The request message must be marked with the [Request] attribute, and the
        /// Response type must match. Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="queueName"></param>
        /// <param name="responseHandler"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        IYieldPoint YieldWithRequestDirect<TRequest, TResponse>(TRequest message, string queueName, Func<TResponse, Task<IYieldPoint>> responseHandler) where TRequest : class where TResponse : class;


        /// <summary>
        /// Publish a request message directly to a queue and continue the flow when the response arrives. 
        /// The exchange and routing key are not used.
        /// The request message must be marked with the [Request] attribute, and the
        /// Response type must match. Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="queueName"></param>
        /// <param name="responseHandler"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        IYieldPoint YieldWithRequestDirect<TRequest, TResponse>(TRequest message, string queueName, Func<TResponse, ValueTask<IYieldPoint>> responseHandler) where TRequest : class where TResponse : class;


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
        IYieldPoint YieldWithRequestSync<TRequest, TResponse>(TRequest message, Func<TResponse, IYieldPoint> responseHandler) where TRequest : class where TResponse : class;


        /// <summary>
        /// Publish a request message directly to a queue and continue the flow when the response arrives. 
        /// The exchange and routing key are not used.
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
        /// <param name="queueName"></param>
        /// <param name="responseHandler"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <returns></returns>
        IYieldPoint YieldWithRequestDirectSync<TRequest, TResponse>(TRequest message, string queueName, Func<TResponse, IYieldPoint> responseHandler) where TRequest : class where TResponse : class;


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
        IYieldPoint EndWithResponse<TResponse>(TResponse message) where TResponse : class;


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
        ValueTask Execute(IFlowHandlerContext context, IYieldPoint yieldPoint);


        /// <summary>
        /// Returns the parallel request for the given message context.
        /// </summary>
        IFlowParallelRequest? GetParallelRequest(IFlowHandlerContext context);


        /// <summary>
        /// Calls the converge method for a parallel flow.
        /// </summary>
        ValueTask Converge(IFlowHandlerContext context);
    }


    /// <summary>
    /// Determines how the Yield method of a parallel request behaves when no requests have been added.
    /// Useful in cases where requests are sent conditionally.
    /// </summary>
    public enum FlowNoRequestsBehaviour
    {
        /// <summary>
        /// Throw an exception. This is the default behaviour to prevent subtle bugs when not specifying the behaviour explicitly,
        /// as well as for backwards compatibility.
        /// </summary>
        Exception,

        /// <summary>
        /// Immediately call the continuation method.
        /// </summary>
        Converge,

        /// <summary>
        /// End the flow without calling the converge method.
        /// </summary>
        EndFlow
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
        IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler) where TRequest : class where TResponse : class;

        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// Note that the response handler can not influence the flow as it does not return a YieldPoint.
        /// It can instead store state in the controller for the continuation passed to the Yield method.
        /// Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, ValueTask> responseHandler) where TRequest : class where TResponse : class;

        /// <remarks>
        /// This overload allows the response handler access to the IFlowParallelRequest interface, which
        /// can be used to add additional requests to the parallel request before the continuation method passed to the Yield method is called.
        /// </remarks>
        /// <inheritdoc cref="AddRequest{TRequest,TResponse}(TRequest,Func{TResponse,Task})"/>
        IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, Task> responseHandler) where TRequest : class where TResponse : class;

        /// <remarks>
        /// This overload allows the response handler access to the IFlowParallelRequest interface, which
        /// can be used to add additional requests to the parallel request before the continuation method passed to the Yield method is called.
        /// </remarks>
        /// <inheritdoc cref="AddRequest{TRequest,TResponse}(TRequest,Func{TResponse,ValueTask})"/>
        IFlowParallelRequestBuilder AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, ValueTask> responseHandler) where TRequest : class where TResponse : class;

        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// Note that the response handler can not influence the flow as it does not return a YieldPoint.
        /// It can instead store state in the controller for the continuation passed to the Yield method.
        /// Used for synchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler) where TRequest : class where TResponse : class;

        /// <remarks>
        /// This overload allows the response handler access to the IFlowParallelRequest interface, which
        /// can be used to add additional requests to the parallel request before the continuation method passed to the Yield method is called.
        /// </remarks>
        /// <inheritdoc cref="AddRequestSync{TRequest,TResponse}(TRequest,Action{TResponse})"/>
        IFlowParallelRequestBuilder AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse, IFlowParallelRequest> responseHandler) where TRequest : class where TResponse : class;

        /// There is no Sync overload with an IFlowParallelRequest parameter, as the AddRequest methods for that are
        /// async, so you should always await them.
        /// <summary>
        /// Constructs an IYieldPoint to continue the flow when responses arrive.
        /// The continuation method is called when all responses have arrived.
        /// Response handlers and the continuation method are guaranteed thread-safe access to the
        /// controller and can store state.
        /// Used for asynchronous continuation methods.
        /// </summary>
        /// <param name="continuation">The converge continuation method to be called when all responses have been handled.</param>
        /// <param name="noRequestsBehaviour">How the Yield method should behave when no requests have been added to the parallel request builder.</param>
        IYieldPoint Yield(Func<Task<IYieldPoint>> continuation, FlowNoRequestsBehaviour noRequestsBehaviour = FlowNoRequestsBehaviour.Exception);

        /// <summary>
        /// Constructs an IYieldPoint to continue the flow when responses arrive.
        /// The continuation method is called when all responses have arrived.
        /// Response handlers and the continuation method are guaranteed thread-safe access to the
        /// controller and can store state.
        /// Used for synchronous continuation methods.
        /// </summary>
        /// <param name="continuation">The converge continuation method to be called when all responses have been handled.</param>
        /// <param name="noRequestsBehaviour">How the Yield method should behave when no requests have been added to the parallel request builder.</param>
        IYieldPoint YieldSync(Func<IYieldPoint> continuation, FlowNoRequestsBehaviour noRequestsBehaviour = FlowNoRequestsBehaviour.Exception);
    }


    /// <summary>
    /// Provides means of adding one or more requests to a parallel request.
    /// </summary>
    /// <remarks>
    /// Add a parameter of this type to a parallel request's response handler to gain access to it's functionality.
    /// Not available in other contexts.
    /// </remarks>
    public interface IFlowParallelRequest
    {
        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// Note that the response handler can not influence the flow as it does not return a YieldPoint.
        /// It can instead store state in the controller for the continuation passed to the Yield method.
        /// Used for asynchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        Task AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, Task> responseHandler) where TRequest : class where TResponse : class;

        /// <remarks>
        /// This overload allows the response handler access to the IFlowParallelRequest interface, which
        /// can be used to add additional requests to the parallel request before the continuation method passed to the Yield method is called.
        /// </remarks>
        /// <inheritdoc cref="AddRequest{TRequest,TResponse}(TRequest,Func{TResponse,Task})"/>
        Task AddRequest<TRequest, TResponse>(TRequest message, Func<TResponse, IFlowParallelRequest, Task> responseHandler) where TRequest : class where TResponse : class;

        /// <summary>
        /// Publish a request message and continue the flow when the response arrives.
        /// Note that the response handler can not influence the flow as it does not return a YieldPoint.
        /// It can instead store state in the controller for the continuation passed to the Yield method.
        /// Used for synchronous response handlers.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseHandler"></param>
        Task AddRequestSync<TRequest, TResponse>(TRequest message, Action<TResponse> responseHandler) where TRequest : class where TResponse : class;
    }


    /// <summary>
    /// Defines if and how the Flow should continue. Construct using any of the IFlowProvider methods.
    /// </summary>
    public interface IYieldPoint
    {
    }
}

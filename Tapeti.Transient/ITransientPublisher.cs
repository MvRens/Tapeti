using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Transient
{
    /// <summary>
    /// Provides a publisher which can send request messages, and await the response on a dynamic queue.
    /// </summary>
    public interface ITransientPublisher
    {
        /// <summary>
        /// Sends a request and waits for the response with the timeout specified in the WithTransient config call.
        /// </summary>
        /// <param name="request"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request) where TRequest : class where TResponse : class;
    }
}
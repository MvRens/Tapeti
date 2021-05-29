using System.Threading.Tasks;

namespace Tapeti.Transient
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation of ITransientPublisher
    /// </summary>
    internal class TransientPublisher : ITransientPublisher
    {
        private readonly TransientRouter router;
        private readonly IPublisher publisher;


        /// <summary>
        /// </summary>
        public TransientPublisher(TransientRouter router, IPublisher publisher)
        {
            this.router = router;
            this.publisher = publisher;
        }


        /// <inheritdoc />
        public async Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request)
        {
            return (TResponse)await router.RequestResponse(publisher, request);
        }
    }
}

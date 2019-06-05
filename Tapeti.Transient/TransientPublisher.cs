using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti.Transient
{
    public class TransientPublisher : ITransientPublisher
    {
        private readonly TransientRouter router;
        private readonly IPublisher publisher;

        public TransientPublisher(TransientRouter router, IPublisher publisher)
        {
            this.router = router;
            this.publisher = publisher;
        }

        public async Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request)
        {
            return (TResponse)(await router.RequestResponse(publisher, request));
        }
    }
}

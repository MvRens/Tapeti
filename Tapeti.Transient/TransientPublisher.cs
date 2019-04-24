using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti.Transient
{
    public class TransientPublisher : ITransientPublisher
    {
        private readonly TransientRouter router;

        public TransientPublisher(TransientRouter router)
        {
            this.router = router;
        }

        public async Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request)
        {
            return (TResponse)(await router.RequestResponse(request));
        }
    }
}

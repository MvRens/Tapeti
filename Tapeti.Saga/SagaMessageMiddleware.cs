using System;
using Tapeti.Config;

namespace Tapeti.Saga
{
    public class SagaMessageMiddleware : IMessageMiddleware
    {
        private readonly IDependencyResolver dependencyResolver;


        public SagaMessageMiddleware(IDependencyResolver dependencyResolver)
        {
            this.dependencyResolver = dependencyResolver;
        }

        public void Handle(IMessageContext context, Action next)
        {
            context.Items["Saga"] = dependencyResolver.Resolve<ISagaProvider>().Continue("");
            next();
        }
    }
}

using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Saga
{
    public class SagaMiddleware : IMiddlewareBundle
    {
        public IEnumerable<object> GetContents(IDependencyResolver dependencyResolver)
        {
            (dependencyResolver as IDependencyInjector)?.RegisterDefault<ISagaProvider, SagaProvider>();

            yield return new SagaBindingMiddleware();
            yield return new SagaMessageMiddleware(dependencyResolver);
        }
    }
}

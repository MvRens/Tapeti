using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Flow.SQL
{
    public static class ConfigExtensions
    {
        public static TapetiConfig WithFlowSqlRepository(this TapetiConfig config)
        {
            config.Use(new FlowSqlRepositoryBundle());
            return config;
        }
    }


    internal class FlowSqlRepositoryBundle : ITapetiExtension
    {
        /*
        public IEnumerable<object> GetContents(IDependencyResolver dependencyResolver)
        {
            ((IDependencyContainer)dependencyResolver)?.RegisterDefault<IFlowRepository, >();

            return null;
        }
        */

        public void RegisterDefaults(IDependencyContainer container)
        {
        }

        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return null;
        }
    }
}

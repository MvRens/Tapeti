using System.Collections.Generic;
using Tapeti.Config;

namespace Tapeti.Flow.SQL
{
    public static class ConfigExtensions
    {
        public static TapetiConfig WithFlowSqlRepository(this TapetiConfig config, string connectionString, int serviceId, string schema = "dbo")
        {
            config.Use(new FlowSqlRepositoryBundle(connectionString, serviceId, schema));
            return config;
        }
    }


    internal class FlowSqlRepositoryBundle : ITapetiExtension
    {
        private readonly string connectionString;
        private readonly string schema;
        private readonly int serviceId;


        public FlowSqlRepositoryBundle(string connectionString, int serviceId, string schema)
        {
            this.connectionString = connectionString;
            this.serviceId = serviceId;
            this.schema = schema;
        }


        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefault<IFlowRepository>(() => new SqlConnectionFlowRepository(connectionString, serviceId, schema));
        }


        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return null;
        }
    }
}

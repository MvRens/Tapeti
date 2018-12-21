using System.Collections.Generic;
using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Flow.SQL
{
    public static class ConfigExtensions
    {
        public static TapetiConfig WithFlowSqlRepository(this TapetiConfig config, string connectionString, string tableName = "Flow")
        {
            config.Use(new FlowSqlRepositoryBundle(connectionString, tableName));
            return config;
        }
    }


    internal class FlowSqlRepositoryBundle : ITapetiExtension
    {
        private readonly string connectionString;
        private readonly string tableName;


        public FlowSqlRepositoryBundle(string connectionString, string tableName)
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
        }


        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefaultSingleton<IFlowRepository>(() => new SqlConnectionFlowRepository(connectionString, tableName));
        }


        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return null;
        }
    }
}

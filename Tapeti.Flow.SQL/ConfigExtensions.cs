using System.Collections.Generic;
using Tapeti.Config;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// Extends ITapetiConfigBuilder to enable Flow SQL.
    /// </summary>
    public static class ConfigExtensions
    {
        /// <summary>
        /// Enables the Flow SQL repository.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        public static ITapetiConfigBuilder WithFlowSqlRepository(this ITapetiConfigBuilder config, string connectionString, string tableName = "Flow")
        {
            config.Use(new FlowSqlRepositoryExtension(connectionString, tableName));
            return config;
        }
    }


    internal class FlowSqlRepositoryExtension : ITapetiExtension
    {
        private readonly string connectionString;
        private readonly string tableName;


        public FlowSqlRepositoryExtension(string connectionString, string tableName)
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

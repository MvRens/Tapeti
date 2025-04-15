using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Enables the single instance Flow SQL repository.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        [Obsolete("Use WithFlowSqlStoreSingleInstanceCached or WithFlowSqlStoreMultiInstance")]
        public static ITapetiConfigBuilder WithFlowSqlRepository(this ITapetiConfigBuilder config, string connectionString, string tableName = "Flow")
        {
            return WithFlowSqlStoreSingleInstanceCached(config, new SqlSingleInstanceCachedFlowStore.Config(connectionString)
            {
                FlowTableName = tableName
            });
        }


        /// <summary>
        /// Enables the single instance Flow SQL repository.
        /// </summary>
        public static ITapetiConfigBuilder WithFlowSqlStoreSingleInstanceCached(this ITapetiConfigBuilder config, SqlSingleInstanceCachedFlowStore.Config storeConfig)
        {
            config.Use(new FlowSqlStoreExtension(c => new SqlSingleInstanceCachedFlowStore(c, storeConfig)));
            return config;
        }


        /// <summary>
        /// Enables the multi instance Flow SQL repository.
        /// </summary>
        public static ITapetiConfigBuilder WithFlowSqlStoreMultiInstance(this ITapetiConfigBuilder config, SqlMultiInstanceFlowStore.Config storeConfig)
        {
            config.Use(new FlowSqlStoreExtension(c => new SqlMultiInstanceFlowStore(c, storeConfig)));
            return config;
        }
    }


    internal class FlowSqlStoreExtension : ITapetiExtension
    {
        private readonly Func<ITapetiConfig, IDurableFlowStore> factory;


        public FlowSqlStoreExtension(Func<ITapetiConfig, IDurableFlowStore> factory)
        {
            this.factory = factory;
        }


        public void RegisterDefaults(IDependencyContainer container)
        {
            container.RegisterDefaultSingleton(() => factory(container.Resolve<ITapetiConfig>()));
        }


        public IEnumerable<object> GetMiddleware(IDependencyResolver dependencyResolver)
        {
            return Enumerable.Empty<object>();
        }
    }
}

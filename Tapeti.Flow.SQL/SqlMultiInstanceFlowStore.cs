using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// <see cref="IDurableFlowStore"/> implementation for SQL server which is compatible with multiple instances of this
    /// service running and processing flows. All locking is performed in the SQL database, and no data is cached.
    /// </summary>
    public class SqlMultiInstanceFlowStore : IDurableFlowStore
    {
        private readonly Config config;

        /// <summary>
        /// Describes the configuration for <see cref="SqlMultiInstanceFlowStore"/> implementations.
        /// </summary>
        public class Config
        {
            /// <summary>
            /// The connection string used for connecting to the SQL Server database.
            /// </summary>
            public string ConnectionString { get; }


            /// <summary>
            /// The table name where flows are stored. Can include a schema. Defaults to "Flow".
            /// </summary>
            public string FlowTableName { get; set; } = "Flow";


            /// <summary>
            /// The table name where continuation lookups are stored. Can include a schema. Defaults to "FlowContinuation".
            /// </summary>
            public string ContinuationsTableName { get; set; } = "FlowContinuation";


            /// <summary>
            /// The table name where locks are stored. Can include a schema. Defaults to "FlowLock".
            /// </summary>
            public string LocksTableName { get; set; } = "FlowLock";
            

            /// <inheritdoc cref="Config"/>
            public Config(string connectionString)
            {
                ConnectionString = connectionString;
            }
        }


        /// <inheritdoc cref="SqlMultiInstanceFlowStore"/>>
        public SqlMultiInstanceFlowStore(Config config)
        {
            this.config = config;
        }


        /// <inheritdoc />
        public ValueTask Load()
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public ValueTask<IFlowStateLock> LockNewFlowState(Guid? newFlowID = null)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public ValueTask<IFlowStateLock?> LockFlowStateByContinuation(Guid continuationID)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(TimeSpan minimumAge)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public ValueTask RemoveState(Guid flowID)
        {
            throw new NotImplementedException();
        }
    }
}

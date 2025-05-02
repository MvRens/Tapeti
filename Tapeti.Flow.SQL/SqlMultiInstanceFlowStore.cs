using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Tapeti.Flow.Default;

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// <see cref="IDurableFlowStore"/> implementation for SQL server which is compatible with multiple instances of this
    /// service running and processing flows. All locking is performed in the SQL database, and no data is cached.
    /// </summary>
    public class SqlMultiInstanceFlowStore : IDurableFlowStore
    {
        private readonly Config storeConfig;

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


            /// <summary>
            /// How often a lock is refreshed to signal to other consumers that it is still being held.
            /// </summary>
            /// <remarks>
            /// Ideally, each flow operation completes within this interval so a refresh is never required.
            /// </remarks>
            public TimeSpan LockRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);


            /// <summary>
            /// How long after the last refresh before a lock is considered to be stale and can be acquired by another consumer.
            /// </summary>
            /// <remarks>
            /// Must be at least equal to <see cref="LockRefreshInterval"/>, recommended is twice that amount.
            /// </remarks>
            public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(60);


            /// <summary>
            /// How long to wait before retrying to acquire a lock which is held by another consumer.
            /// </summary>
            public TimeSpan LockRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
            

            /// <inheritdoc cref="Config"/>
            public Config(string connectionString)
            {
                ConnectionString = connectionString;
            }
        }


        private volatile bool loaded;


        /// <inheritdoc cref="SqlMultiInstanceFlowStore"/>>
        public SqlMultiInstanceFlowStore(Config storeConfig)
        {
            this.storeConfig = storeConfig;


            if (storeConfig.LockTimeout < storeConfig.LockRefreshInterval)
                throw new InvalidOperationException("LockTimeout must be equal to or greater than LockRefreshInterval");
        }


        /// <inheritdoc />
        public ValueTask Load()
        {
            // Enforce Load being called even though it is a no-op in this current implementation
            loaded = true;
            return default;
        }


        /// <inheritdoc />
        public async ValueTask<IFlowStateLock> LockNewFlowState(Guid? newFlowID = null)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            var flowID = newFlowID ?? Guid.NewGuid();
            return await AcquireLock(flowID);
        }


        /// <inheritdoc />
        public async ValueTask<IFlowStateLock?> LockFlowStateByContinuation(Guid continuationID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            var flowID = await GetFlowIDByContinuation(continuationID);
            return flowID is not null ? await AcquireLock(flowID.Value) : null;
        }


        /// <inheritdoc />
        public ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(DateTime? maxCreationTime)
        {
            throw new NotImplementedException();
        }


        private Task<Guid?> GetFlowIDByContinuation(Guid continuationID)
        {
            return SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"select FlowID from {storeConfig.ContinuationsTableName} where ContinuationID = @ContinuationID", connection);

                var continuationIDParam = query.Parameters.Add("@ContinuationID", SqlDbType.UniqueIdentifier);
                continuationIDParam.Value = continuationID;

                return (Guid?)await query.ExecuteScalarAsync();
            });
        }


        private Task<IFlowStateLock> AcquireLock(Guid flowID)
        {
            var lockID = Guid.NewGuid();

            return SqlRetryHelper.Execute(async () =>
            {
                while (true)
                {
                    await using var connection = await GetConnection().ConfigureAwait(false);

                    var query = new SqlCommand($"""
                                                set transaction isolation level serializable;
                                                begin transaction;
                                                
                                                declare @clusteringID bigint;
                                                declare @currentLockID uniqueidentifier;
                                                declare @refreshTime datetime2(3);
                                                
                                                select
                                                  @clusteringID = ClusteringID,
                                                  @currentLockID = LockID,
                                                  @refreshTime = RefreshTime
                                                from
                                                  {storeConfig.LocksTableName} with (updlock)
                                                where
                                                  FlowID = @FlowID;

                                                if @@ROWCOUNT = 0
                                                begin
                                                    insert into {storeConfig.LocksTableName} (FlowID, LockID, AcquireTime, RefreshTime)
                                                    values (@FlowID, @LockID, @Now, @Now);
                                                    
                                                    select 
                                                        @LockID, 
                                                        (select StateJson from {storeConfig.FlowTableName} where FlowID = @FlowID);
                                                end else if @currentLockID = @LockID
                                                begin
                                                    select 
                                                        @LockID, 
                                                        (select StateJson from {storeConfig.FlowTableName} where FlowID = @FlowID);
                                                end else if @refreshTime < @Timeout
                                                begin
                                                    update {storeConfig.LocksTableName}
                                                    set
                                                        LockID = @LockID,
                                                        AcquireTime = @Now,
                                                        RefreshTime = @Now
                                                    where
                                                        ClusteringID = @clusteringID;
                                                    
                                                    select 
                                                        @LockID, 
                                                        (select StateJson from {storeConfig.FlowTableName} where FlowID = @FlowID);
                                                end else
                                                begin
                                                    select
                                                        @currentLockID,
                                                        null;
                                                end

                                                commit transaction;
                                                """, connection);

                    var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                    var lockIDParam = query.Parameters.Add("@LockID", SqlDbType.UniqueIdentifier);
                    var nowParam = query.Parameters.Add("@Now", SqlDbType.DateTime2);
                    var timeoutParam = query.Parameters.Add("@Timeout", SqlDbType.DateTime2);

                    flowIDParam.Value = flowID;
                    lockIDParam.Value = lockID;

                    var now = DateTime.UtcNow;
                    nowParam.Value = now;
                    timeoutParam.Value = now.Subtract(storeConfig.LockTimeout);

                    await using var reader = await query.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var currentLockID = reader.GetGuid(0);

                        if (currentLockID == lockID)
                        {
                            var stateJson = reader.IsDBNull(1) ? null : reader.GetString(1);
                            var state = stateJson == null ? null : JsonConvert.DeserializeObject<FlowState>(stateJson);

                            return new FlowStateLock(this, flowID, lockID, state) as IFlowStateLock;
                        }
                    }

                    // TODO log
                    await Task.Delay(storeConfig.LockRetryInterval);
                }
            });
        }


        private Task<bool> RefreshLock(Guid flowID, Guid lockID)
        {
            return SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"""
                                            set transaction isolation level serializable;
                                            begin transaction;
                                            
                                            update {storeConfig.LocksTableName}
                                            set
                                              RefreshTime = @Now
                                            where
                                              FlowID = @FlowID and
                                              LockID = @LockID;

                                            commit transaction;
                                            """, connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                var lockIDParam = query.Parameters.Add("@LockID", SqlDbType.UniqueIdentifier);
                var nowParam = query.Parameters.Add("@Now", SqlDbType.DateTime2);

                flowIDParam.Value = flowID;
                lockIDParam.Value = lockID;
                nowParam.Value = DateTime.UtcNow;

                return await query.ExecuteNonQueryAsync() > 0;
            });
        }


        private async ValueTask RemoveState(Guid flowID)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"""
                                           delete from {storeConfig.FlowTableName} where FlowID = @FlowID;
                                           delete from {storeConfig.ContinuationsTableName} where FlowID = @FlowID;
                                           """, connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                flowIDParam.Value = flowID;

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private Task StoreFlowState(Guid flowID, Guid lockID, FlowState? oldFlowState, FlowState newFlowState)
        {
            return SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                return false;
            });



            // Update the lookup dictionary for the ContinuationIDs
            /*
            if (cachedFlowState?.FlowState != null)
            {
                foreach (var removedContinuation in cachedFlowState.FlowState.Continuations.Keys.Where(k => !newFlowState.Continuations.ContainsKey(k)))
                    continuationLookup.TryRemove(removedContinuation, out _);
            }

            foreach (var addedContinuation in newFlowState.Continuations.Where(c => cachedFlowState?.FlowState == null || !cachedFlowState.FlowState.Continuations.ContainsKey(c.Key)))
                continuationLookup.TryAdd(addedContinuation.Key, flowID);

            var newCachedFlowState = new CachedFlowState(newFlowState, cachedFlowState?.CreationTime ?? DateTime.UtcNow);
            flowStates[flowID] = newCachedFlowState;

            if (cachedFlowState == null)
                await CreateState(flowID, newFlowState, newCachedFlowState.CreationTime).ConfigureAwait(false);
            else
                await UpdateState(flowID, cachedFlowState.FlowState).ConfigureAwait(false);

            return newCachedFlowState;
            */
        }


        private async Task CreateState(Guid flowID, FlowState flowState, DateTime creationTime)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"insert into {storeConfig.FlowTableName} (FlowID, StateJson, CreationTime)" +
                                           "values (@FlowID, @StateJson, @CreationTime)",
                    connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                var stateJsonParam = query.Parameters.Add("@StateJson", SqlDbType.NVarChar);
                var creationTimeParam = query.Parameters.Add("@CreationTime", SqlDbType.DateTime2);

                flowIDParam.Value = flowID;
                stateJsonParam.Value = JsonConvert.SerializeObject(flowState);
                creationTimeParam.Value = creationTime;

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private async Task UpdateState(Guid flowID, FlowState flowState)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"update {storeConfig.FlowTableName} set StateJson = @StateJson where FlowID = @FlowID", connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                var stateJsonParam = query.Parameters.Add("@StateJson", SqlDbType.NVarChar);

                flowIDParam.Value = flowID;
                stateJsonParam.Value = JsonConvert.SerializeObject(flowState);

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private async Task<SqlConnection> GetConnection()
        {
            var connection = new SqlConnection(storeConfig.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            return connection;
        }


        private class FlowStateLock : IFlowStateLock
        {
            private readonly SqlMultiInstanceFlowStore owner;
            private readonly Guid lockID;
            private FlowState? flowState;

            public bool disposed;
            public Guid FlowID { get; }


            public FlowStateLock(SqlMultiInstanceFlowStore owner, Guid flowID, Guid lockID, FlowState? flowState)
            {
                this.owner = owner;
                this.lockID = lockID;
                this.flowState = flowState;

                FlowID = flowID;

                // TODO timer to refresh lock while held
            }


            public ValueTask DisposeAsync()
            {
                disposed = true;

                // TODO remove lock

                return default;
            }


            public FlowState? GetFlowState()
            {
                ObjectDisposedException.ThrowIf(disposed, "FlowStateLock");

                return flowState;
            }


            // ReSharper disable once ParameterHidesMember
            public async ValueTask StoreFlowState(FlowState flowState, bool persistent)
            {
                ObjectDisposedException.ThrowIf(disposed, "FlowStateLock");

                this.flowState = flowState;
                await owner.StoreFlowState(FlowID, lockID, flowState, flowState);
            }


            public ValueTask DeleteFlowState()
            {
                ObjectDisposedException.ThrowIf(disposed, "FlowStateLock");

                return owner.RemoveState(FlowID);
            }
        }
    }
}

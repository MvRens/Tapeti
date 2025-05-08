using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Tapeti.Flow.Default;

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// Thrown when a lock has been forcefully acquired by another instance due to a timeout.
    /// </summary>
    public class LockLostException : Exception
    {
        /// <summary>
        /// The FlowID for which the lock was previously acquired.
        /// </summary>
        public Guid FlowID { get; }

        /// <summary>
        /// The ID which identifies the lock which was previously acquired.
        /// </summary>
        public Guid LockID { get; }

        /// <inheritdoc />
        public LockLostException(Guid flowID, Guid lockID) : base($"LockID {lockID} for FlowID {flowID} is no longer valid")
        {
            FlowID = flowID;
            LockID = lockID;
        }
    }


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
                query.Parameters.AddWithValue("@ContinuationID", continuationID).SqlDbType = SqlDbType.UniqueIdentifier;

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

                    var now = DateTime.UtcNow;

                    query.Parameters.AddWithValue("@FlowID", flowID).SqlDbType = SqlDbType.UniqueIdentifier;
                    query.Parameters.AddWithValue("@LockID", lockID).SqlDbType = SqlDbType.UniqueIdentifier;
                    query.Parameters.AddWithValue("@Now", now).SqlDbType = SqlDbType.DateTime2;
                    query.Parameters.AddWithValue("@Timeout", now.Subtract(storeConfig.LockTimeout)).SqlDbType = SqlDbType.DateTime2;

                    await using var reader = await query.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var currentLockID = reader.GetGuid(0);

                        if (currentLockID == lockID)
                        {
                            var stateJson = reader.IsDBNull(1) ? null : reader.GetString(1);
                            var state = stateJson == null ? null : JsonConvert.DeserializeObject<FlowState>(stateJson);

                            return new FlowStateLock(this, flowID, lockID, state, storeConfig.LockRefreshInterval) as IFlowStateLock;
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

                query.Parameters.AddWithValue("@FlowID", flowID).SqlDbType = SqlDbType.UniqueIdentifier;
                query.Parameters.AddWithValue("@LockID", lockID).SqlDbType = SqlDbType.UniqueIdentifier;
                query.Parameters.AddWithValue("@Now", DateTime.UtcNow).SqlDbType = SqlDbType.DateTime2;

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

                query.Parameters.AddWithValue("@FlowID", flowID).SqlDbType = SqlDbType.UniqueIdentifier;

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private Task<bool> StoreFlowState(Guid flowID, Guid lockID, FlowState flowState)
        {
            var continuations = new DataTable();
            continuations.Columns.Add("ContinuationID", typeof(Guid));
            continuations.Columns.Add("ContinuationMethod", typeof(string));

            foreach (var continuation in flowState.Continuations)
                continuations.Rows.Add(continuation.Key, continuation.Value.MethodName);


            var creationTime = DateTime.UtcNow;
            var stateJson = JsonConvert.SerializeObject(flowState);


            return SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"""
                                            set transaction isolation level serializable;
                                            begin transaction;
                                            
                                            if exists (select LockID from {storeConfig.LocksTableName} with (updlock) where FlowID = @FlowID and LockID = @LockID)
                                            begin
                                                merge {storeConfig.FlowTableName} as t
                                                using (values (@FlowID, @CreationTime, @StateJson)) as s (FlowID, CreationTime, StateJson)
                                                on t.FlowID = s.FlowID
                                                
                                                when matched then
                                                    update set StateJson = s.StateJson
                                                    
                                                when not matched then
                                                    insert (FlowID, CreationTime, StateJson)
                                                    values (s.FlowID, s.CreationTime, s.StateJson);
                                            
                                            
                                                merge {storeConfig.ContinuationsTableName} as t
                                                using @Continuations as s
                                                on t.ContinuationID = s.ContinuationID and t.FlowID = @FlowID
                                                
                                                when not matched by target then
                                                    insert (ContinuationID, FlowID, ContinuationMethod)
                                                    values (s.ContinuationID, @FlowID, s.ContinuationMethod)
                                                    
                                                when not matched by source and t.FlowID = @FlowID then
                                                    delete;
                                                    
                                                select 1;
                                            end else
                                            begin
                                                select 0;
                                            end

                                            commit transaction;
                                            """, connection);

                query.Parameters.AddWithValue("@FlowID", flowID).SqlDbType = SqlDbType.UniqueIdentifier;
                query.Parameters.AddWithValue("@LockID", lockID).SqlDbType = SqlDbType.UniqueIdentifier;
                query.Parameters.AddWithValue("@CreationTime", creationTime).SqlDbType = SqlDbType.DateTime2;
                query.Parameters.AddWithValue("@StateJson", stateJson).SqlDbType = SqlDbType.NVarChar;
                
                var continuationsParam = query.Parameters.AddWithValue("@Continuations", continuations);
                continuationsParam.SqlDbType = SqlDbType.Structured;
                continuationsParam.TypeName = "FlowContinuationType";

                var result = await query.ExecuteScalarAsync().ConfigureAwait(false);
                return result is not null && (int)result > 0;
            });
        }


        private async Task ReleaseFlowLock(Guid flowID, Guid lockID)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"delete from {storeConfig.LocksTableName} where FlowID = @FlowID and LockID = @LockID", connection);

                var flowIDParam = query.Parameters.AddWithValue("@FlowID", SqlDbType.UniqueIdentifier);
                var lockIDParam = query.Parameters.AddWithValue("@LockID", SqlDbType.UniqueIdentifier);

                flowIDParam.Value = flowID;
                lockIDParam.Value = lockID;

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
            private readonly TimeSpan lockRefreshInterval;
            private bool disposed;

            private readonly CancellationTokenSource refreshTaskCancellation = new();
            private bool lockHeld = true;

            public Guid FlowID { get; }


            public FlowStateLock(SqlMultiInstanceFlowStore owner, Guid flowID, Guid lockID, FlowState? flowState, TimeSpan lockRefreshInterval)
            {
                this.owner = owner;
                this.lockID = lockID;
                this.flowState = flowState;
                this.lockRefreshInterval = lockRefreshInterval;

                FlowID = flowID;

                _ = Task.Run(RefreshLock, CancellationToken.None);
            }


            public async ValueTask DisposeAsync()
            {
                if (!disposed && lockHeld)
                    await owner.ReleaseFlowLock(FlowID, lockID);

                await refreshTaskCancellation.CancelAsync();

                disposed = true;
            }


            public FlowState? GetFlowState()
            {
                CheckPrerequisites();

                return flowState;
            }


            // ReSharper disable once ParameterHidesMember
            public async ValueTask StoreFlowState(FlowState flowState, bool persistent)
            {
                CheckPrerequisites();

                this.flowState = flowState;
                if (!await owner.StoreFlowState(FlowID, lockID, flowState))
                    throw new LockLostException(FlowID, lockID);
            }


            public ValueTask DeleteFlowState()
            {
                CheckPrerequisites();

                return owner.RemoveState(FlowID);
            }


            private async Task RefreshLock()
            {
                while (!refreshTaskCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(lockRefreshInterval, refreshTaskCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // ReSharper disable once InvertIf
                    if (!await owner.RefreshLock(FlowID, lockID))
                    {
                        lockHeld = false;
                        break;
                    }
                }
            }

            private void CheckPrerequisites()
            {
                ObjectDisposedException.ThrowIf(disposed, "FlowStateLock");

                if (!lockHeld)
                    throw new LockLostException(FlowID, lockID);
            }
        }
    }
}

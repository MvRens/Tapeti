using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Flow.Default;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// <see cref="IDurableFlowStore"/> implementation for SQL server which assumes there is only one instance of the
    /// service running and processing flows. This was the default implementation for Tapeti.Flow.SQL 3.3.0 and before, and can still
    /// be useful.<br/>
    /// <br/>
    /// Advantages:<br/>
    /// - Locks can be acquired instantly in-memory<br/>
    /// - The amount of SQL queries is reduced<br/>
    /// - All flows are loaded in memory and can be validated at startup<br/>
    /// <br/>
    /// Disadvantages:<br/>
    /// - Not compatible with running multiple instances<br/>
    /// - Memory usage and startup time are affected by long-running flows<br/>
    /// </summary>
    public class SqlSingleInstanceCachedFlowStore : IDurableFlowStore
    {
        /// <summary>
        /// Describes the configuration for <see cref="SqlSingleInstanceCachedFlowStore"/> implementations.
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


            /// <inheritdoc cref="Config"/>
            public Config(string connectionString)
            {
                ConnectionString = connectionString;
            }
        }



        private class CachedFlowState
        {
            public readonly FlowState FlowState;
            public readonly DateTime CreationTime;


            public CachedFlowState(FlowState flowState, DateTime creationTime)
            {
                FlowState = flowState;
                CreationTime = creationTime;
            }
        }

        private readonly Config config;
        private readonly ConcurrentDictionary<Guid, CachedFlowState> flowStates = new();
        private readonly ConcurrentDictionary<Guid, Guid> continuationLookup = new();
        private readonly LockCollection<Guid> locks = new(EqualityComparer<Guid>.Default);

        private volatile bool loadStarted;
        private volatile bool loaded;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        public SqlSingleInstanceCachedFlowStore(Config config)
        {
            this.config = config;
        }

        /// <inheritdoc />
        public async ValueTask Load()
        {
            if (loadStarted)
                return;

            loadStarted = true;

            await SqlRetryHelper.Execute(async () =>
            {
                await using var connection = await GetConnection().ConfigureAwait(false);

                var flowQuery = new SqlCommand($"select FlowID, CreationTime, StateJson from {config.FlowTableName}", connection);
                var flowReader = await flowQuery.ExecuteReaderAsync().ConfigureAwait(false);

                while (await flowReader.ReadAsync().ConfigureAwait(false))
                {
                    var flowID = flowReader.GetGuid(0);
                    var creationTime = flowReader.GetDateTime(1);
                    var stateJson = flowReader.GetString(2);

                    var state = JsonConvert.DeserializeObject<FlowState>(stateJson);
                    if (state != null)
                        AddToCache(flowID, new CachedFlowState(state, creationTime));
                }
            }).ConfigureAwait(false);

            loaded = true;
        }


        /// <inheritdoc />
        public ValueTask<IFlowStateLock> LockNewFlowState(Guid? newFlowID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            var flowID = newFlowID ?? Guid.NewGuid();
            return GetFlowStateLock(flowID, true);
        }


        /// <inheritdoc />
        public async ValueTask<IFlowStateLock?> LockFlowStateByContinuation(Guid continuationID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            return continuationLookup.TryGetValue(continuationID, out var flowID)
                ? await GetFlowStateLock(flowID, false)
                : null;
        }


        private async ValueTask<IFlowStateLock> GetFlowStateLock(Guid flowID, bool isNew)
        {
            var flowLock = await locks.GetLock(flowID).ConfigureAwait(false);
            var cachedFlowState = isNew ? null : flowStates.GetValueOrDefault(flowID);

            return new FlowStateLock(this, flowID, flowLock, cachedFlowState);
        }


        /// <inheritdoc />
        public ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(TimeSpan minimumAge)
        {
            throw new NotImplementedException();
        }


        private ValueTask RemoveState(Guid flowID)
        {
            throw new NotImplementedException();
        }



        private void AddToCache(Guid flowID, CachedFlowState cachedFlowState)
        {
            flowStates[flowID] = cachedFlowState;

            foreach (var continuation in cachedFlowState.FlowState.Continuations)
                continuationLookup.TryAdd(continuation.Key, flowID);
        }


        private CachedFlowState StoreFlowState(Guid flowID, CachedFlowState? cachedFlowState, FlowState newFlowState)
        {
            // Update the lookup dictionary for the ContinuationIDs
            if (cachedFlowState?.FlowState != null)
            {
                foreach (var removedContinuation in cachedFlowState.FlowState.Continuations.Keys.Where(k => !newFlowState.Continuations.ContainsKey(k)))
                    continuationLookup.TryRemove(removedContinuation, out _);
            }

            foreach (var addedContinuation in newFlowState.Continuations.Where(c => cachedFlowState?.FlowState == null || !cachedFlowState.FlowState.Continuations.ContainsKey(c.Key)))
                continuationLookup.TryAdd(addedContinuation.Key, flowID);

            var newCachedFlowState = new CachedFlowState(newFlowState, cachedFlowState?.CreationTime ?? DateTime.UtcNow);
            flowStates[flowID] = newCachedFlowState;

            // TODO persist (create, update)

            return newCachedFlowState;
        }


        private async Task<SqlConnection> GetConnection()
        {
            var connection = new SqlConnection(config.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            return connection;
        }


        private class FlowStateLock : IFlowStateLock
        {
            private readonly SqlSingleInstanceCachedFlowStore owner;
            private IDisposable? flowLock;
            private CachedFlowState? cachedFlowState;

            public Guid FlowID { get; }


            public FlowStateLock(SqlSingleInstanceCachedFlowStore owner, Guid flowID, IDisposable flowLock, CachedFlowState? cachedFlowState)
            {
                this.owner = owner;
                this.flowLock = flowLock;
                this.cachedFlowState = cachedFlowState;

                FlowID = flowID;
            }


            public void Dispose()
            {
                var l = flowLock;
                flowLock = null;
                l?.Dispose();
            }


            public ValueTask<FlowState?> GetFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return new ValueTask<FlowState?>(cachedFlowState?.FlowState);
            }


            public ValueTask StoreFlowState(FlowState flowState, bool persistent)
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                cachedFlowState = owner.StoreFlowState(FlowID, cachedFlowState, flowState);
                return default;
            }


            public ValueTask DeleteFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return owner.RemoveState(FlowID);
            }
        }
    }
}

/*
public async ValueTask<IEnumerable<FlowRecord<T>>> GetStates<T>()
{
    return await SqlRetryHelper.Execute(async () =>
    {
        using var connection = await GetConnection().ConfigureAwait(false);

        var flowQuery = new SqlCommand($"select FlowID, CreationTime, StateJson from {tableName}", connection);
        var flowReader = await flowQuery.ExecuteReaderAsync().ConfigureAwait(false);

        var result = new List<FlowRecord<T>>();

        while (await flowReader.ReadAsync().ConfigureAwait(false))
        {
            var flowID = flowReader.GetGuid(0);
            var creationTime = flowReader.GetDateTime(1);
            var stateJson = flowReader.GetString(2);

            var state = JsonConvert.DeserializeObject<T>(stateJson);
            if (state != null)
                result.Add(new FlowRecord<T>(flowID, creationTime, state));
        }

        return result;
    }).ConfigureAwait(false);
}

/// <inheritdoc />
public async ValueTask CreateState<T>(Guid flowID, T state, DateTime timestamp)
{
    await SqlRetryHelper.Execute(async () =>
    {
        using var connection = await GetConnection().ConfigureAwait(false);

        var query = new SqlCommand($"insert into {tableName} (FlowID, StateJson, CreationTime)" +
                                   "values (@FlowID, @StateJson, @CreationTime)",
            connection);

        var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
        var stateJsonParam = query.Parameters.Add("@StateJson", SqlDbType.NVarChar);
        var creationTimeParam = query.Parameters.Add("@CreationTime", SqlDbType.DateTime2);

        flowIDParam.Value = flowID;
        stateJsonParam.Value = JsonConvert.SerializeObject(state);
        creationTimeParam.Value = timestamp;

        await query.ExecuteNonQueryAsync().ConfigureAwait(false);
    }).ConfigureAwait(false);
}

/// <inheritdoc />
public async ValueTask UpdateState<T>(Guid flowID, T state)
{
    await SqlRetryHelper.Execute(async () =>
    {
        using var connection = await GetConnection().ConfigureAwait(false);

        var query = new SqlCommand($"update {tableName} set StateJson = @StateJson where FlowID = @FlowID", connection);

        var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
        var stateJsonParam = query.Parameters.Add("@StateJson", SqlDbType.NVarChar);

        flowIDParam.Value = flowID;
        stateJsonParam.Value = JsonConvert.SerializeObject(state);

        await query.ExecuteNonQueryAsync().ConfigureAwait(false);
    }).ConfigureAwait(false);
}

/// <inheritdoc />
public async ValueTask DeleteState(Guid flowID)
{
    await SqlRetryHelper.Execute(async () =>
    {
        using var connection = await GetConnection().ConfigureAwait(false);

        var query = new SqlCommand($"delete from {tableName} where FlowID = @FlowID", connection);

        var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
        flowIDParam.Value = flowID;

        await query.ExecuteNonQueryAsync().ConfigureAwait(false);
    }).ConfigureAwait(false);
}
*/
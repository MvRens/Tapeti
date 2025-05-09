using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Shouldly;
using Tapeti.Flow;
using Tapeti.Flow.Default;
using Xunit;

namespace Tapeti.Tests.Flow.SQL
{
    public abstract class BaseSqlFlowStoreTest
    {
        internal readonly SQLTestHelper TestHelper;


        protected BaseSqlFlowStoreTest(SQLFixture fixture, string databasePrefix)
        {
            TestHelper = new SQLTestHelper(fixture, databasePrefix);
        }



        protected virtual async Task PrepareLoadAndGetActiveFlows(SqlConnection connection, Guid flowId, DateTime creationTime, string stateJson)
        {
            await connection.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) values (@flowId, @creationTime, @stateJson)",
                new
                {
                    flowId,
                    creationTime,
                    stateJson
                });
        }


        [Fact]
        public async Task LoadAndGetActiveFlows()
        {
            var (connectionString, flowStore) = await CreateDatabaseAndFlowStore("LoadAndGetActiveFlows");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var flowId = new Guid("ebc7b8d8-f475-40e4-a974-b768d1fe35bd");
            var creationTime = new DateTime(2025, 4, 24, 13, 37, 42, DateTimeKind.Utc);
            const string stateJson = "{}";

            await PrepareLoadAndGetActiveFlows(connection, flowId, creationTime, stateJson);
            await flowStore.Load();


            var flows = (await flowStore.GetActiveFlows()).Where(f => f.FlowID == flowId).ToArray();
            flows.Length.ShouldBe(1, "minCreationTime = null");

            flows = (await flowStore.GetActiveFlows(creationTime.AddSeconds(-1))).Where(f => f.FlowID == flowId).ToArray();
            flows.Length.ShouldBe(0, "minCreationTime = -1s");
        }




        [Fact]
        public async Task LockNewFlowState()
        {
            var (connectionString, flowStore) = await CreateDatabaseAndFlowStore("LockNewFlowState");
            await using var connection = new SqlConnection(connectionString);

            await flowStore.Load();

            var flowId = new Guid("d1d3c51d-4f8a-480c-8cb7-c01cf7b13522");

            await using var flowStateLock = await flowStore.LockNewFlowState(flowId);
            flowStateLock.FlowID.ShouldBe(flowId);
            flowStateLock.GetFlowState().ShouldBeNull();

            await CheckExists(connection, flowId, false);
            await flowStateLock.StoreFlowState(new FlowState(), true);
            await CheckExists(connection, flowId, true);
        }


        protected virtual async Task PrepareLockFlowStateByContinuation(SqlConnection connection, Guid flowId, DateTime creationTime, string stateJson, Guid continuationId)
        {
            await connection.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) values (@flowId, @creationTime, @stateJson)",
                new
                {
                    flowId,
                    creationTime,
                    stateJson
                });
        }


        [Fact]
        public async Task LockFlowStateByContinuation()
        {
            var (connectionString, flowStore) = await CreateDatabaseAndFlowStore("LockFlowStateByContinuation");
            await using var connection = new SqlConnection(connectionString);

            var flowId = new Guid("609cf011-3ebb-4f2f-ac3a-f4673f54ff97");
            var continuationId = new Guid("cdcf573a-59f3-4008-a218-a6cfb3d413e7");
            var creationTime = new DateTime(2025, 4, 24, 13, 37, 42, DateTimeKind.Utc);
            var stateJson = JsonConvert.SerializeObject(new FlowState
            {
                Continuations = new Dictionary<Guid, ContinuationMetadata>
                {
                    { continuationId, new ContinuationMetadata() }
                }
            });


            await PrepareLockFlowStateByContinuation(connection, flowId, creationTime, stateJson, continuationId);
            await flowStore.Load();

            var flowStateLock = await flowStore.LockFlowStateByContinuation(continuationId);
            flowStateLock.ShouldNotBeNull();
            flowStateLock.FlowID.ShouldBe(flowId);

            var flowState = flowStateLock.GetFlowState();
            flowState.ShouldNotBeNull();
            flowState.Continuations.ShouldContainKey(continuationId);
        }


        // TODO test multiple locks on a single flow



        internal abstract Task<string> CreateDatabase(string databaseTestName);
        internal abstract IDurableFlowStore CreateFlowStore(string connectionString);

        
        private async Task<(string, IDurableFlowStore)> CreateDatabaseAndFlowStore(string databaseTestName)
        {
            var connectionString = await CreateDatabase(databaseTestName);
            var flowStore = CreateFlowStore(connectionString);

            return (connectionString, flowStore);
        }


        private static async Task CheckExists(SqlConnection connection, Guid flowId, bool expectedExists)
        {
            var count = await connection.ExecuteScalarAsync<int>("select count(*) from Flow where FlowID = @flowId", new { flowId });
            count.ShouldBe(expectedExists ? 1 : 0);
        }
    }
}

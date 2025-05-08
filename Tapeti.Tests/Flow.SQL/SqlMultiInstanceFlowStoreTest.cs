using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Shouldly;
using SimpleInjector;
using Tapeti.Flow.Default;
using Tapeti.Flow.SQL;
using Tapeti.SimpleInjector;
using Xunit;

namespace Tapeti.Tests.Flow.SQL
{
    [Collection(SQLCollection.Name)]
    [Trait("Category", "Requires Docker")]
    public class SqlMultiInstanceFlowStoreTest
    {
        private readonly SQLTestHelper testHelper;
        private readonly Container container = new();


        public SqlMultiInstanceFlowStoreTest(SQLFixture fixture)
        {
            testHelper = new SQLTestHelper(fixture, "MultiInstance");
        }



        [Fact]
        public async Task LoadAndGetActiveFlows()
        {
            var (connectionString, flowStore) = await CreateDatabaseAndFlowStore("LoadAndGetActiveFlows");
            await using var connection = new SqlConnection(connectionString);


            var flowId = new Guid("854fd0be-113b-41eb-8121-82691945b962");
            var creationTime = new DateTime(2025, 4, 24, 13, 37, 42, DateTimeKind.Utc);
            const string stateJson = "{}";

            await connection.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) values (@flowId, @creationTime, @stateJson)",
                new
                {
                    flowId,
                    creationTime,
                    stateJson
                });

            await flowStore.Load();


            var flows = (await flowStore.GetActiveFlows(null));
            flows.Count().ShouldBe(1, "minCreationTime = null");

            flows = (await flowStore.GetActiveFlows(creationTime.AddSeconds(-1)));
            flows.Count().ShouldBe(0, "minCreationTime = -1s");
        }


        [Fact]
        public async Task LockNewFlowState()
        {
            var (connectionString, flowStore) = await CreateDatabaseAndFlowStore("LockNewFlowState");
            await using var connection = new SqlConnection(connectionString);

            await flowStore.Load();

            var flowId = new Guid("e1b32d65-6c9a-4466-a898-913730791282");

            await using var flowStateLock = await flowStore.LockNewFlowState(flowId);
            flowStateLock.FlowID.ShouldBe(flowId);
            flowStateLock.GetFlowState().ShouldBeNull();

            await CheckExists(connection, flowId, false);
            await flowStateLock.StoreFlowState(new FlowState(), true);
            await CheckExists(connection, flowId, true);
        }


        [Fact]
        public async Task LockFlowStateByContinuation()
        {
            var (connectionString, flowStore) = await CreateDatabaseAndFlowStore("LockFlowStateByContinuation");
            await using var connection = new SqlConnection(connectionString);

            var flowId = new Guid("ca0eafac-be2e-4f0c-ad6f-7133536d175c");
            var continuationId = new Guid("5cfa20a6-cdf9-495f-a834-081c333f68f7");
            var creationTime = new DateTime(2025, 4, 24, 13, 37, 42, DateTimeKind.Utc);
            var stateJson = JsonConvert.SerializeObject(new FlowState
            {
                Continuations = new Dictionary<Guid, ContinuationMetadata>
                {
                    { continuationId, new ContinuationMetadata() }
                }
            });

            await connection.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) values (@flowId, @creationTime, @stateJson);" +
                "insert into FlowContinuation (ContinuationID, FlowID, ContinuationMethod) values (@continuationID, @flowId, 'TestMethod');",
                new
                {
                    flowId,
                    creationTime,
                    stateJson,
                    continuationId
                });



            await flowStore.Load();
            var flowStateLock = await flowStore.LockFlowStateByContinuation(continuationId);
            flowStateLock.ShouldNotBeNull();
            flowStateLock.FlowID.ShouldBe(flowId);

            var flowState = flowStateLock.GetFlowState();
            flowState.ShouldNotBeNull();
            flowState.Continuations.ShouldContainKey(continuationId);
        }


        [Fact]
        public async Task ConvertToMultiInstance()
        {
            Debug.Assert(false);

            // TODO set up clean database
            //await TapetiFlowSqlMetadata.UpdateForMultiInstanceStore(connection, true);
        }



        private SqlMultiInstanceFlowStore CreateFlowStore(string connectionString)
        {
            return new SqlMultiInstanceFlowStore(new SqlMultiInstanceFlowStore.Config(connectionString));
        }


        private async Task<(string, SqlMultiInstanceFlowStore)> CreateDatabaseAndFlowStore(string databaseTestName)
        {
            var connectionString = await testHelper.GetMultiInstanceDatabase(databaseTestName);
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

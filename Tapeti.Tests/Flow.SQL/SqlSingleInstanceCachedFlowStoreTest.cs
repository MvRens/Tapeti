using System;
using System.Collections.Generic;
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
    public class SqlSingleInstanceCachedFlowStoreTest : IAsyncLifetime
    {
        private readonly SQLFixture fixture;
        private readonly SqlSingleInstanceCachedFlowStore flowStore;
        private readonly Container container = new();

        private SqlConnection? connection;


        public SqlSingleInstanceCachedFlowStoreTest(SQLFixture fixture)
        {
            this.fixture = fixture;

            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .Build();

            flowStore = new SqlSingleInstanceCachedFlowStore(config, new SqlSingleInstanceCachedFlowStore.Config(fixture.ConnectionString));
        }


        public async Task InitializeAsync()
        {
            connection = new SqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();

            await TapetiFlowSqlMetadata.UpdateForSingleInstanceCachedStore(connection);
        }


        public async Task DisposeAsync()
        {
            if (connection is not null)
                await connection.CloseAsync();
        }


        [Fact]
        public async Task LoadAndGetActiveFlows()
        {
            var flowId = new Guid("ebc7b8d8-f475-40e4-a974-b768d1fe35bd");
            var creationTime = new DateTime(2025, 4, 24, 13, 37, 42, DateTimeKind.Utc);
            const string stateJson = "{}";

            await connection!.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) values (@flowId, @creationTime, @stateJson)",
                new
                {
                    flowId,
                    creationTime,
                    stateJson
                });

            await flowStore.Load();


            var flows = await flowStore.GetActiveFlows(null);
            flows.Count().ShouldBe(1, "minCreationTime = null");

            flows = await flowStore.GetActiveFlows(creationTime.AddSeconds(-1));
            flows.Count().ShouldBe(0, "minCreationTime = -1s");
        }


        [Fact]
        public async Task LockNewFlowState()
        {
            await flowStore.Load();

            var flowId = new Guid("d1d3c51d-4f8a-480c-8cb7-c01cf7b13522");

            using var flowStateLock = await flowStore.LockNewFlowState(flowId);
            flowStateLock.FlowID.ShouldBe(flowId);
            flowStateLock.GetFlowState().ShouldBeNull();

            await CheckExists(flowId, false);
            await flowStateLock.StoreFlowState(new FlowState(), true);
            await CheckExists(flowId, true);
        }


        [Fact]
        public async Task LockFlowStateByContinuation()
        {
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

            await connection!.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) values (@flowId, @creationTime, @stateJson)",
                new
                {
                    flowId,
                    creationTime,
                    stateJson
                });



            await flowStore.Load();
            var flowStateLock = await flowStore.LockFlowStateByContinuation(continuationId);
            flowStateLock.ShouldNotBeNull();
            flowStateLock.FlowID.ShouldBe(flowId);

            var flowState = flowStateLock.GetFlowState();
            flowState.ShouldNotBeNull();
            flowState.Continuations.ShouldContainKey(continuationId);
        }


        private async Task CheckExists(Guid flowId, bool expectedExists)
        {
            var count = await connection!.ExecuteScalarAsync<int>("select count(*) from Flow where FlowID = @flowId", new { flowId });
            count.ShouldBe(expectedExists ? 1 : 0);
        }
    }
}

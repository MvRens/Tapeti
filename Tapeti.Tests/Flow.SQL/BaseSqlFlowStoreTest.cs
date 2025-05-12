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
using Tapeti.Tests.Helpers;
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



        [Fact]
        public async Task LocksOnDifferentFlows()
        {
            var (_, flowStore) = await CreateDatabaseAndFlowStore("LocksOnDifferentFlows");
            await flowStore.Load();


            var lock1Acquired = new TaskCompletionSource();
            var lock2Acquired = new TaskCompletionSource();
            var releaseLocks = new TaskCompletionSource();


            var task1 = Task.Run(async () =>
            {
                var flowID1 = new Guid("290e3af4-285c-44dd-98ac-72a551d5dc04");
                var flowLock = await flowStore.LockNewFlowState(flowID1);

                lock1Acquired.SetResult();
                await releaseLocks.Task;

                await flowLock.DisposeAsync();
            });

            var task2 = Task.Run(async () =>
            {
                var flowID2 = new Guid("b090c46c-7dd5-4c1d-a091-b5e6b1342921");
                var flowLock = await flowStore.LockNewFlowState(flowID2);

                lock2Acquired.SetResult();
                await releaseLocks.Task;

                await flowLock.DisposeAsync();
            });


            var taskTimeout = TimeSpan.FromSeconds(10);

            await Task.WhenAll(
                lock1Acquired.Task.WithTimeout(taskTimeout, "Lock1Acquired"),
                lock2Acquired.Task.WithTimeout(taskTimeout, "Lock2Acquired")
            );

            releaseLocks.SetResult();

            await Task.WhenAll(
                task1.WithTimeout(taskTimeout, "Task1"), 
                task2.WithTimeout(taskTimeout, "Task2")
            );
        }


        [Fact]
        public async Task LocksOnSameFlow()
        {
            var (_, flowStore) = await CreateDatabaseAndFlowStore("LocksOnSameFlow");
            await flowStore.Load();


            var flowID = new Guid("c98d5fe5-7e21-4e67-a96d-bb745aa657c5");
            var lock1Acquired = new TaskCompletionSource();
            var lock1Released = new TaskCompletionSource();
            var lock2Acquiring = new TaskCompletionSource();
            var lock2Acquired = new TaskCompletionSource();
            var releaseLock1 = new TaskCompletionSource();
            var releaseLock2 = new TaskCompletionSource();


            // Note: this relies on the fact that all implementations treat LockNewFlowState with a predetermined FlowID
            // like any lock on that FlowID, and is not expected to actually be new. If implementations are added which
            // have different behaviour for good reasons, changes to this test and more setup may be required.
            var task1 = Task.Run(async () =>
            {
                var flowLock = await flowStore.LockNewFlowState(flowID);

                lock1Acquired.SetResult();
                await releaseLock1.Task;

                await flowLock.DisposeAsync();
                lock1Released.SetResult();
            });

            var task2 = Task.Run(async () =>
            {
                await lock1Acquired.Task;
                lock2Acquiring.SetResult();

                var flowLock = await flowStore.LockNewFlowState(flowID);

                lock2Acquired.SetResult();
                await releaseLock2.Task;

                await flowLock.DisposeAsync();
            });


            var taskTimeout = TimeSpan.FromSeconds(10);


            await lock1Acquired.Task.WithTimeout(taskTimeout, "Lock1Acquired");
            await lock2Acquiring.Task.WithTimeout(taskTimeout, "Lock2Acquiring");

            await Task.Delay(100);

            lock2Acquired.Task.IsCompleted.ShouldBeFalse(); 

            releaseLock1.SetResult();
            await lock2Acquired.Task.WithTimeout(taskTimeout, "Lock2Acquired");

            lock1Released.Task.IsCompleted.ShouldBeTrue();
            releaseLock2.SetResult(); 


            await Task.WhenAll(
                task1.WithTimeout(taskTimeout, "Task1"),
                task2.WithTimeout(taskTimeout, "Task2")
            );
        }



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

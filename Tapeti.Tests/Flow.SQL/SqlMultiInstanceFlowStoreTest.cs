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
using Tapeti.Flow.SQL;
using Tapeti.Tests.Mock;
using Xunit;

namespace Tapeti.Tests.Flow.SQL
{
    [Collection(SQLCollection.Name)]
    [Trait("Category", "Requires Docker")]
    public class SqlMultiInstanceFlowStoreTest : BaseSqlFlowStoreTest
    {
        public SqlMultiInstanceFlowStoreTest(SQLFixture fixture) : base(fixture, "MultiInstance")
        {
        }


        protected override async Task PrepareLockFlowStateByContinuation(SqlConnection connection, Guid flowId, DateTime creationTime, string stateJson, Guid continuationId)
        {
            await base.PrepareLockFlowStateByContinuation(connection, flowId, creationTime, stateJson, continuationId);

            await connection.ExecuteAsync(
                "insert into FlowContinuation (ContinuationID, FlowID, ContinuationMethod) values (@continuationID, @flowId, 'TestMethod');",
                new
                {
                    flowId,
                    creationTime,
                    stateJson,
                    continuationId
                });
        }


        [Fact]
        public async Task ConvertToMultiInstance()
        {
            var connectionString = await TestHelper.GetSingleInstanceCachedDatabase("ConvertToMultiInstance");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();


            var flowId1 = new Guid("89882f38-4eb4-4dd5-a398-318511886787");
            var continuationId1 = new Guid("b350848d-76a0-4726-bfa7-352caab097ab");
            var continuationId2 = new Guid("6925f0d1-240d-4b10-bbd6-a609c15f22e5");
            var creationTime1 = new DateTime(2025, 5, 9, 9, 12, 0, DateTimeKind.Utc);
            var stateJson1 = JsonConvert.SerializeObject(new FlowState
            {
                Continuations = new Dictionary<Guid, ContinuationMetadata>
                {
                    {
                        continuationId1, new ContinuationMetadata
                        {
                            MethodName = "Continuation1",
                            ConvergeMethodName = "ConvergeMethod1",
                            ConvergeMethodSync = true
                        }
                    },
                    {
                        continuationId2, new ContinuationMetadata
                        {
                            MethodName = "Continuation2",
                            ConvergeMethodName = "ConvergeMethod1",
                            ConvergeMethodSync = false
                        }
                    }
                },
                Data = "{\"hello\",\"world\"}",
                Metadata = new FlowMetadata(new ReplyMetadata
                {
                    CorrelationId = "1075e580-9d08-46b0-957f-3aa8b212ba18",
                    Mandatory = true,
                    ReplyTo = "dev.null",
                    ResponseTypeName = "VoidResponseMessage"
                })
            });


            var flowId2 = new Guid("96c3d60f-399f-4c11-80e3-9ae6ad9958be");
            var continuationId3 = new Guid("23478c67-a80c-4184-86d6-c1f5e4413851");
            var creationTime2 = new DateTime(2025, 5, 9, 9, 40, 0, DateTimeKind.Utc);
            var stateJson2 = JsonConvert.SerializeObject(new FlowState
            {
                Continuations = new Dictionary<Guid, ContinuationMetadata>
                {
                    {
                        continuationId3, new ContinuationMetadata
                        {
                            MethodName = "Continuation3"
                        }
                    }
                },
                Metadata = new FlowMetadata(null)
            });

            await connection.ExecuteAsync(
                "insert into Flow (FlowID, CreationTime, StateJson) " +
                "values (@flowId1, @creationTime1, @stateJson1), (@flowId2, @creationTime2, @stateJson2)",
                new
                {
                    flowId1,
                    creationTime1,
                    stateJson1,
                    flowId2,
                    creationTime2,
                    stateJson2
                });


            await TapetiFlowSqlMetadata.UpdateForMultiInstanceStore(connection);


            var countBefore = await connection.ExecuteScalarAsync<int>("select count(*) from FlowContinuation");
            countBefore.ShouldBe(0);

            await TapetiFlowSqlMetadata.ConvertSingleToMultiInstance(connection);

            var continuationsAfter = (await connection.QueryAsync<DbContinuation>("select ContinuationID, FlowID, ContinuationMethod from FlowContinuation")).ToArray();
            continuationsAfter.Length.ShouldBe(3);

            var continuation1 = continuationsAfter.Single(c => c.ContinuationID == continuationId1);
            var continuation2 = continuationsAfter.Single(c => c.ContinuationID == continuationId2);
            var continuation3 = continuationsAfter.Single(c => c.ContinuationID == continuationId3);

            continuationsAfter.ShouldSatisfyAllConditions(
                _ => continuation1.FlowID.ShouldBe(flowId1),
                _ => continuation1.ContinuationMethod.ShouldBe("Continuation1"),

                _ => continuation2.FlowID.ShouldBe(flowId1),
                _ => continuation2.ContinuationMethod.ShouldBe("Continuation2"),

                _ => continuation3.FlowID.ShouldBe(flowId2),
                _ => continuation3.ContinuationMethod.ShouldBe("Continuation3")
            );
        }


        internal override Task<string> CreateDatabase(string databaseTestName)
        {
            return TestHelper.GetMultiInstanceDatabase(databaseTestName);
        }


        internal override IDurableFlowStore CreateFlowStore(string connectionString)
        {
            return new SqlMultiInstanceFlowStore(new MockContinuationMethodValidatorFactory(), new SqlMultiInstanceFlowStore.Config(connectionString));
        }



        #pragma warning disable CS0649
        private class DbContinuation
        {
            public Guid ContinuationID;
            public Guid FlowID;
            public string? ContinuationMethod;
        }
        #pragma warning restore CS0649
    }
}

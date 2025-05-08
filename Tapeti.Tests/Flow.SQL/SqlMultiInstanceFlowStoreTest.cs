using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Tapeti.Flow;
using Tapeti.Flow.SQL;
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
            Debug.Assert(false);

            // TODO set up clean database
            //await TapetiFlowSqlMetadata.UpdateForMultiInstanceStore(connection, true);
        }


        internal override Task<string> CreateDatabase(SQLTestHelper testHelper, string databaseTestName)
        {
            return testHelper.GetMultiInstanceDatabase(databaseTestName);
        }


        internal override IDurableFlowStore CreateFlowStore(string connectionString)
        {
            return new SqlMultiInstanceFlowStore(new SqlMultiInstanceFlowStore.Config(connectionString));
        }
    }
}

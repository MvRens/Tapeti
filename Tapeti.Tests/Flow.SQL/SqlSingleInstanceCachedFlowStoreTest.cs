using System.Threading.Tasks;
using JetBrains.Annotations;
using Tapeti.Flow;
using Tapeti.Flow.SQL;
using Tapeti.Tests.Mock;
using Xunit;

namespace Tapeti.Tests.Flow.SQL
{
    [Collection(SQLCollection.Name)]
    [Trait("Category", "Requires Docker")]
    [UsedImplicitly]
    public class SqlSingleInstanceCachedFlowStoreTest : BaseSqlFlowStoreTest
    {
        public SqlSingleInstanceCachedFlowStoreTest(SQLFixture fixture) : base(fixture, "SingleInstance")
        {
        }


        internal override Task<string> CreateDatabase(string databaseTestName)
        {
            return TestHelper.GetSingleInstanceCachedDatabase(databaseTestName);
        }


        internal override IDurableFlowStore CreateFlowStore(string connectionString)
        {
            return new SqlSingleInstanceCachedFlowStore(new MockContinuationMethodValidatorFactory(), new SqlSingleInstanceCachedFlowStore.Config(connectionString));
        }
    }
}

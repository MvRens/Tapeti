using System.Threading.Tasks;
using JetBrains.Annotations;
using SimpleInjector;
using Tapeti.Flow;
using Tapeti.Flow.SQL;
using Tapeti.SimpleInjector;
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


        internal override Task<string> CreateDatabase(SQLTestHelper testHelper, string databaseTestName)
        {
            return testHelper.GetSingleInstanceCachedDatabase(databaseTestName);
        }


        internal override IDurableFlowStore CreateFlowStore(string connectionString)
        {
            var container = new Container();
            var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                .Build();

            return new SqlSingleInstanceCachedFlowStore(config, new SqlSingleInstanceCachedFlowStore.Config(connectionString));
        }
    }
}

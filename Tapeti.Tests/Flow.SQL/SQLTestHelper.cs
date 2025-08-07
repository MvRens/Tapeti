using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Tapeti.Flow.SQL;

namespace Tapeti.Tests.Flow.SQL
{
    internal class SQLTestHelper
    {
        private readonly SQLFixture fixture;
        private readonly string databasePrefix;


        public SQLTestHelper(SQLFixture fixture, string databasePrefix)
        {
            this.fixture = fixture;
            this.databasePrefix = databasePrefix;
        }


        public Task<string> GetEmptyDatabase(string databaseTestName)
        {
            return fixture.CreateDatabase($"{databasePrefix}-{databaseTestName}");
        }


        public async Task<string> GetSingleInstanceCachedDatabase(string databaseTestName)
        {
            var connectionString = await GetEmptyDatabase(databaseTestName);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await TapetiFlowSqlMetadata.UpdateForSingleInstanceCachedStore(connection);

            return connectionString;
        }


        public async Task<string> GetMultiInstanceDatabase(string databaseTestName)
        {
            var connectionString = await GetEmptyDatabase(databaseTestName);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await TapetiFlowSqlMetadata.UpdateForMultiInstanceStore(connection);

            return connectionString;
        }

    }
}
 
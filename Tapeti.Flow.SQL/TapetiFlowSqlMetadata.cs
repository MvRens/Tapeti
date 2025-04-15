using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global - public API

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// Provides helper methods for initialising a SQL database for use with the Tapeti Flow SQL stores.
    /// </summary>
    /// <remarks>
    /// Assumes the default table names. For customized table names, run your own modified versions of the scripts
    /// as found at <see href="https://github.com/MvRens/Tapeti/tree/develop/Tapeti.Flow.SQL/scripts"/>.
    /// </remarks>
    public static class TapetiFlowSqlMetadata
    {
        /// <summary>
        /// Creates or updates the tables required for <see cref="SqlSingleInstanceCachedFlowStore"/>.
        /// </summary>
        public static async Task UpdateForSingleInstanceCachedStore(SqlConnection connection)
        {
            await UpdateFlowTable(connection);
        }


        /// <summary>
        /// Creates or updates the tables required for <see cref="SqlMultiInstanceFlowStore"/>.
        /// </summary>
        public static async Task UpdateForMultiInstanceStore(SqlConnection connection)
        {
            await UpdateFlowTable(connection);
            await UpdateContinuationAndLockTables(connection);
        }


        private static async Task UpdateFlowTable(SqlConnection connection)
        {
            await RunEmbeddedScript(connection, "Flow table.sql");
        }


        private static async Task UpdateContinuationAndLockTables(SqlConnection connection)
        {
            await RunEmbeddedScript(connection, "Continuation and lock tables.sql");
        }


        private static async Task RunEmbeddedScript(SqlConnection connection, string scriptName)
        {
            string script;
            {
                await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Tapeti.Flow.SQL.scripts.{scriptName}");
                if (stream == null)
                    throw new ArgumentException($"Resource not found: {scriptName}");

                using var streamReader = new StreamReader(stream, Encoding.UTF8);
                script = await streamReader.ReadToEndAsync();
            }


            var query = new SqlCommand(script, connection);
            await query.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}

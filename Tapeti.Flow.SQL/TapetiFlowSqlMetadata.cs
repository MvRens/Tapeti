using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tapeti.Flow.Default;

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
        public static async Task UpdateForSingleInstanceCachedStore(SqlConnection connection, CancellationToken cancellationToken)
        {
            await UpdateFlowTable(connection, cancellationToken);
        }


        /// <inheritdoc cref="UpdateForSingleInstanceCachedStore(SqlConnection, CancellationToken)"/>>
        public static Task UpdateForSingleInstanceCachedStore(SqlConnection connection)
        {
            return UpdateForSingleInstanceCachedStore(connection, CancellationToken.None);
        }


        /// <summary>
        /// Creates or updates the tables required for <see cref="SqlMultiInstanceFlowStore"/>.
        /// </summary>
        public static async Task UpdateForMultiInstanceStore(SqlConnection connection, CancellationToken cancellationToken)
        {
            // TODO conversion from single to multiinstance (driven by default parameter?)

            await UpdateFlowTable(connection, cancellationToken);
            await UpdateContinuationAndLockTables(connection, cancellationToken);
        }


        /// <inheritdoc cref="UpdateForMultiInstanceStore(SqlConnection, CancellationToken)"/>>
        public static Task UpdateForMultiInstanceStore(SqlConnection connection)
        {
            return UpdateForMultiInstanceStore(connection, CancellationToken.None);
        }



        /// <summary>
        /// For services which were initially using the SingleInstance (or pre-Tapeti-3.3.1) FlowStore, convert
        /// all flows in the database to be compatible with the MultiInstance store.
        /// </summary>
        /// <remarks>
        /// It is safe to call this method more than once, but it is recommended to call it only once for the
        /// migration due to the startup cost if there are a lot of active flows.
        /// </remarks>
        public static async Task ConvertSingleToMultiInstance(SqlConnection connection, CancellationToken cancellationToken)
        {
            var continuations = new List<DbFlowContinuation>();
            var selectQuery = new SqlCommand("select FlowID, StateJson from Flow where FlowID not in (select FlowID from FlowContinuation)", connection);

            await using (var reader = await selectQuery.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var flowID = reader.GetGuid(0);
                    var stateJson = reader.IsDBNull(1) ? null : reader.GetString(1);

                    if (string.IsNullOrEmpty(stateJson))
                        // TODO log?
                        continue;

                    var state = JsonConvert.DeserializeObject<FlowState>(stateJson);
                    if (state is null)
                        // TODO log?
                        continue;

                    continuations.AddRange(state.Continuations.Select(p => new DbFlowContinuation
                    {
                        ContinuationID = p.Key,
                        FlowID = flowID,
                        MethodName = p.Value.MethodName
                    }));
                }
            }


            var insertQuery = new SqlCommand("insert into FlowContinuation (ContinuationID, FlowID, ContinuationMethod) values (@ContinuationID, @FlowID, @MethodName)", connection);
            var continuationIDParam = insertQuery.Parameters.Add("@ContinuationID", SqlDbType.UniqueIdentifier);
            var flowIDParam = insertQuery.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
            var methodNameParam = insertQuery.Parameters.Add("@MethodName", SqlDbType.NVarChar);

            foreach (var flow in continuations.GroupBy(c => c.FlowID))
            {
                // Transaction per flow, so this method can be run again to recover the failed conversions. Otherwise if one of the continuations
                // is inserted but the second for the same flow is not it will not be converted again and end up corrupt.
                var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    foreach (var continuation in flow)
                    {
                        continuationIDParam.Value = continuation.ContinuationID;
                        flowIDParam.Value = continuation.FlowID;
                        methodNameParam.Value = continuation.MethodName;

                        insertQuery.Transaction = transaction;
                        await insertQuery.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
        }


        /// <inheritdoc cref="ConvertSingleToMultiInstance(SqlConnection, CancellationToken)"/>>
        public static Task ConvertSingleToMultiInstance(SqlConnection connection)
        {
            return ConvertSingleToMultiInstance(connection, CancellationToken.None);
        }


        private static async Task UpdateFlowTable(SqlConnection connection, CancellationToken cancellationToken)
        {
            await RunEmbeddedScript(connection, "Flow table.sql", cancellationToken);
        }


        private static async Task UpdateContinuationAndLockTables(SqlConnection connection, CancellationToken cancellationToken)
        {
            await RunEmbeddedScript(connection, "Continuation and lock tables.sql", cancellationToken);
        }


        private static async Task RunEmbeddedScript(SqlConnection connection, string scriptName, CancellationToken cancellationToken)
        {
            string script;
            {
                await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Tapeti.Flow.SQL.scripts.{scriptName}");
                if (stream == null)
                    throw new ArgumentException($"Resource not found: {scriptName}");

                using var streamReader = new StreamReader(stream, Encoding.UTF8);
                script = await streamReader.ReadToEndAsync(cancellationToken);
            }


            var query = new SqlCommand(script, connection);
            await query.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // TODO validatemethods for multiinstance


        private class DbFlowContinuation
        {
            public required Guid ContinuationID { get; init; }
            public required Guid FlowID { get; init; }
            public required string? MethodName { get; init; }
        }
    }
}

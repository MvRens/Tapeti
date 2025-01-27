using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

// Neither of these are available in language version 7 required for .NET Standard 2.0
// ReSharper disable ConvertToUsingDeclaration
// ReSharper disable UseAwaitUsing

namespace Tapeti.Flow.SQL
{
    /// <summary>
    /// IFlowRepository implementation for SQL server.
    /// </summary>
    /// <remarks>
    /// Assumes the following table layout (table name configurable and may include schema):
    /// create table Flow
    /// (
    ///     FlowID uniqueidentifier not null,
    ///     CreationTime datetime2(3) not null,
    ///     StateJson nvarchar(max) null,
    ///     constraint PK_Flow primary key clustered(FlowID)
    /// );
    /// </remarks>
    public class SqlConnectionFlowRepository : IFlowRepository
    {
        private readonly string connectionString;
        private readonly string tableName;


        /// <summary>
        /// </summary>
        public SqlConnectionFlowRepository(string connectionString, string tableName = "Flow")
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
        }


        /// <inheritdoc />
        public async ValueTask<IEnumerable<FlowRecord<T>>> GetStates<T>()
        {
            return await SqlRetryHelper.Execute(async () =>
            {
                using var connection = await GetConnection().ConfigureAwait(false);

                var flowQuery = new SqlCommand($"select FlowID, CreationTime, StateJson from {tableName}", connection);
                var flowReader = await flowQuery.ExecuteReaderAsync().ConfigureAwait(false);

                var result = new List<FlowRecord<T>>();

                while (await flowReader.ReadAsync().ConfigureAwait(false))
                {
                    var flowID = flowReader.GetGuid(0);
                    var creationTime = flowReader.GetDateTime(1);
                    var stateJson = flowReader.GetString(2);

                    var state = JsonConvert.DeserializeObject<T>(stateJson);
                    if (state != null)
                        result.Add(new FlowRecord<T>(flowID, creationTime, state));
                }

                return result;
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask CreateState<T>(Guid flowID, T state, DateTime timestamp)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"insert into {tableName} (FlowID, StateJson, CreationTime)" +
                                           "values (@FlowID, @StateJson, @CreationTime)",
                    connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                var stateJsonParam = query.Parameters.Add("@StateJson", SqlDbType.NVarChar);
                var creationTimeParam = query.Parameters.Add("@CreationTime", SqlDbType.DateTime2);

                flowIDParam.Value = flowID;
                stateJsonParam.Value = JsonConvert.SerializeObject(state);
                creationTimeParam.Value = timestamp;

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask UpdateState<T>(Guid flowID, T state)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                using var connection = await GetConnection().ConfigureAwait(false);
                 
                var query = new SqlCommand($"update {tableName} set StateJson = @StateJson where FlowID = @FlowID", connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                var stateJsonParam = query.Parameters.Add("@StateJson", SqlDbType.NVarChar);

                flowIDParam.Value = flowID;
                stateJsonParam.Value = JsonConvert.SerializeObject(state);

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DeleteState(Guid flowID)
        {
            await SqlRetryHelper.Execute(async () =>
            {
                using var connection = await GetConnection().ConfigureAwait(false);

                var query = new SqlCommand($"delete from {tableName} where FlowID = @FlowID", connection);

                var flowIDParam = query.Parameters.Add("@FlowID", SqlDbType.UniqueIdentifier);
                flowIDParam.Value = flowID;

                await query.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private async Task<SqlConnection> GetConnection()
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            return connection;
        }
    }
}

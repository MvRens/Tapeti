using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tapeti.Flow.SQL
{
    /*
        Assumes the following table layout (schema configurable):
         
     
        create table shared.Flow
        (
	        FlowID uniqueidentifier not null,
	        ServiceID int not null,
	        CreationTime datetime2(3) not null,
	        StateJson nvarchar(max) null,

	        constraint PK_Flow primary key clustered (FlowID)
        );
        go;
    */
    public class SqlConnectionFlowRepository : IFlowRepository
    {
        private readonly string connectionString;
        private readonly int serviceId;
        private readonly string schema;


        public SqlConnectionFlowRepository(string connectionString, int serviceId, string schema)
        {
            this.connectionString = connectionString;
            this.serviceId = serviceId;
            this.schema = schema;
        }


        public async Task<List<KeyValuePair<Guid, T>>> GetStates<T>()
        {
            using (var connection = await GetConnection())
            {
                var flowQuery = new SqlCommand($"select FlowID, StateJson from {schema}.Flow " +
                                                "where ServiceID = @ServiceID ",
                                                connection);
                var flowServiceParam = flowQuery.Parameters.Add("@ServiceID", SqlDbType.Int);

                flowServiceParam.Value = serviceId;


                var flowReader = await flowQuery.ExecuteReaderAsync();

                var result = new List<KeyValuePair<Guid, T>>();

                while (await flowReader.ReadAsync())
                {
                    var flowID = flowReader.GetGuid(0);
                    var stateJson = flowReader.GetString(1);

                    var state = JsonConvert.DeserializeObject<T>(stateJson);
                    result.Add(new KeyValuePair<Guid, T>(flowID, state));
                }

                return result;
            }

        }

        public Task CreateState<T>(Guid flowID, T state, DateTime timestamp)
        {
            var stateJason = JsonConvert.SerializeObject(state);

            throw new NotImplementedException();
        }

        public Task UpdateState<T>(Guid flowID, T state)
        {
            throw new NotImplementedException();
        }

        public Task DeleteState(Guid flowID)
        {
            throw new NotImplementedException();
        }


        private async Task<SqlConnection> GetConnection()
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            return connection;
        }
    }
}

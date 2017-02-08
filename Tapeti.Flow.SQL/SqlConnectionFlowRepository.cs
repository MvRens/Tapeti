using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.SQL
{
    /*
        Assumes the following table layout (schema configurable):
         
     
        create table dbo.Flow
        (
	        FlowID uniqueidentifier not null,
	        ServiceID int not null,
	        CreationTime datetime2(3) not null,
	        Metadata nvarchar(max) null,
	        Flowdata nvarchar(max) null,

	        constraint PK_Flow primary key clustered (FlowID)
        );
	
        create table dbo.FlowContinuation
        (
	        FlowID uniqueidentifier not null,
	        ContinuationID uniqueidentifier not null,
	        Metadata nvarchar(max) null,

	        constraint PK_FlowContinuation primary key clustered (FlowID, ContinuationID)
        );
        go;

        alter table shared.FlowContinuation with check add constraint FK_FlowContinuation_Flow foreign key (FlowID) references shared.Flow (FlowID);
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


        public async Task<IQueryable<FlowStateRecord>> GetStates()
        {
            var result = new List<FlowStateRecord>();

            using (var connection = await GetConnection())
            {
                var flowQuery = new SqlCommand($"select FlowID, Metadata, Flowdata from {schema}.Flow " +
                                                "where ServiceID = @ServiceID " +
                                                "order by FlowID", connection);
                var flowServiceParam = flowQuery.Parameters.Add("@ServiceID", SqlDbType.Int);

                var continuationQuery = new SqlCommand($"select FlowID, ContinuationID, Metadata from {schema}.FlowContinuation " +
                                                        "where ServiceID = @ServiceID " +
                                                        "order by FlowID", connection);
                var continuationQueryParam = flowQuery.Parameters.Add("@ServiceID", SqlDbType.Int);


                flowServiceParam.Value = serviceId;
                continuationQueryParam.Value = serviceId;


                var flowReader = await flowQuery.ExecuteReaderAsync();
                var continuationReader = await continuationQuery.ExecuteReaderAsync();
                var hasContinuation = await continuationReader.ReadAsync();

                while (await flowReader.ReadAsync())
                {
                    var flowStateRecord = new FlowStateRecord
                    {
                        FlowID = flowReader.GetGuid(0),
                        Metadata = flowReader.GetString(1),
                        Data = flowReader.GetString(2),
                        ContinuationMetadata = new Dictionary<Guid, string>()
                    };

                    while (hasContinuation && continuationReader.GetGuid(0) == flowStateRecord.FlowID)
                    {
                        flowStateRecord.ContinuationMetadata.Add(
                            continuationReader.GetGuid(1),
                            continuationReader.GetString(2)
                        );

                        hasContinuation = await continuationReader.ReadAsync();
                    }

                    result.Add(flowStateRecord);
                }
            }

            return result.AsQueryable();
        }


        public Task CreateState(FlowStateRecord stateRecord, DateTime timestamp)
        {
            throw new NotImplementedException();
        }

        public Task UpdateState(FlowStateRecord stateRecord)
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

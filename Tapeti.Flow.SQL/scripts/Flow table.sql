/*

	This script is embedded in the Tapeti.Flow.SQL package so it can be used with, for example, DbUp

*/
if object_id(N'dbo.Flow', N'U') is null
begin
    create table Flow
    (
         ClusteringID bigint identity(1,1) not null,
         FlowID uniqueidentifier not null,
         CreationTime datetime2(3) not null,
         StateJson nvarchar(max) null,
         constraint PK_Flow primary key nonclustered(FlowID)
    );

    create clustered index CI_Flow_Clustering on Flow (ClusteringID);
end
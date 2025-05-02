/*

	This script is embedded in the Tapeti.Flow.SQL package so it can be used with, for example, DbUp

*/
if object_id(N'dbo.Continuation', N'U') is null
begin
    create table FlowContinuation
    (
         ClusteringID bigint identity(1,1) not null,
         ContinuationID uniqueidentifier not null,
         FlowID uniqueidentifier not null,
         ContinuationMethod nvarchar(255) not null,
         constraint PK_FlowContinuation primary key nonclustered(ContinuationID, FlowID)
    );

    create clustered index CI_FlowContinuation_Clustering on FlowContinuation (ClusteringID);
end

if object_id(N'dbo.FlowLock', N'U') is null
begin
    create table FlowLock
    (
         ClusteringID bigint identity(1,1) not null,
         FlowID uniqueidentifier not null,
         LockID uniqueidentifier not null,
         AcquireTime datetime2(3) not null,
         RefreshTime datetime2(3) not null,
         constraint PK_FlowLock primary key nonclustered(FlowID)
    );

    create clustered index CI_FlowLock on FlowLock (ClusteringID);
end
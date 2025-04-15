/*

	This script is embedded in the Tapeti.Flow.SQL package so it can be used with, for example, DbUp

*/
if object_id(N'dbo.Flow', N'U') is null
begin
    create table Flow
    (
         FlowID uniqueidentifier not null,
         CreationTime datetime2(3) not null,
         StateJson nvarchar(max) null,
         constraint PK_Flow primary key clustered(FlowID)
    );
end
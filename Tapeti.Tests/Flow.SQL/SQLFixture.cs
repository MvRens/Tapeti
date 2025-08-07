using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using Dapper;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

namespace Tapeti.Tests.Flow.SQL
{
    [CollectionDefinition(Name)]
    public sealed class SQLCollection : ICollectionFixture<SQLFixture>
    {
        public const string Name = "SQL";
    }


    public sealed class SQLFixture : IAsyncLifetime
    {
        private INetwork? network;
        private IContainer? sqlContainer;

        private ushort sqlPort;

        private const int DefaultSQLPort = 1433;


        public async Task InitializeAsync()
        {
            network = new NetworkBuilder()
                .WithName(Guid.NewGuid().ToString("D"))
                .Build();

            // If you get a "Sequence contains no elements" error here: make sure Docker Desktop is running
            sqlContainer = new MsSqlBuilder()
                .WithNetwork(network)
                .WithNetworkAliases("sql")
                .Build();

            await network.CreateAsync();
            await sqlContainer.StartAsync();

            sqlPort = sqlContainer.GetMappedPublicPort(DefaultSQLPort);
        }


        public async Task DisposeAsync()
        {
            if (sqlContainer != null)
                await sqlContainer.DisposeAsync();

            if (network != null)
                await network.DeleteAsync();
        }


        public async Task<string> CreateDatabase(string name)
        {
            if (sqlContainer is null)
                throw new InvalidOperationException("SQL container not running");

            await using var connection = new SqlConnection(GetConnectionString(MsSqlBuilder.DefaultDatabase));
            await connection.ExecuteAsync($"create database \"{name}\"");

            return GetConnectionString(name);
        }


        private string GetConnectionString(string databaseName)
        {
            Contract.Assume(sqlContainer is not null);

            return $"data source={sqlContainer.Hostname},{sqlPort};initial catalog={databaseName};user id={MsSqlBuilder.DefaultUsername};password={MsSqlBuilder.DefaultPassword};TrustServerCertificate=True";
        }
    }
}
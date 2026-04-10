using Dapper;
using Npgsql;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.FaultHandling;
using Pipelinez.PostgreSql.Client;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.Internal;
using Pipelinez.PostgreSql.Mapping;
using Pipelinez.PostgreSql.Tests.EndToEnd.Models;
using Xunit;

namespace Pipelinez.PostgreSql.Tests.Unit;

public sealed class PostgreSqlTableMapTests
{
    [Fact]
    public void PostgreSqlTableMap_Generated_Command_Uses_Quoted_Identifiers_And_Parameters()
    {
        var tableMap = PostgreSqlTableMap<TestPostgreSqlRecord>.ForTable("app data", "processed-orders")
            .Map("order id", record => record.Id)
            .MapJson("payload", record => record);

        var command = PostgreSqlMappedCommandFactory.CreateCommand(
            tableMap,
            new TestPostgreSqlRecord
            {
                Id = "A-100",
                Value = "good"
            },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
            defaultCommandTimeoutSeconds: 30);

        Assert.Equal(
            "INSERT INTO \"app data\".\"processed-orders\" (\"order id\", \"payload\") VALUES (@p0, @p1::jsonb)",
            command.CommandText);

        var parameters = Assert.IsType<DynamicParameters>(command.Parameters);
        Assert.Equal("A-100", parameters.Get<string>("p0"));
        Assert.Contains("\"id\":\"A-100\"", parameters.Get<string>("p1"), StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSqlDestinationOptions_Validate_Rejects_Ambiguous_DataSource_Configuration()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;Username=postgres;Password=postgres");

        var options = new PostgreSqlDestinationOptions
        {
            DataSource = dataSource,
            ConnectionString = "Host=localhost;Database=test;Username=postgres;Password=postgres"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("cannot combine", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostgreSqlConnectionFactory_Does_Not_Dispose_Externally_Owned_DataSource()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;Username=postgres;Password=postgres");
        var factory = new PostgreSqlConnectionFactory(new PostgreSqlDestinationOptions
        {
            DataSource = dataSource
        });

        await factory.DisposeAsync().ConfigureAwait(false);

        using var command = dataSource.CreateCommand("SELECT 1");
        Assert.Equal("SELECT 1", command.CommandText);
    }

    [Fact]
    public void PostgreSqlTableMap_Validate_Requires_At_Least_One_Column()
    {
        var tableMap = PostgreSqlTableMap<TestPostgreSqlRecord>.ForTable("app", "orders");

        var exception = Assert.Throws<InvalidOperationException>(() => tableMap.Validate());
        Assert.Contains("at least one column", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

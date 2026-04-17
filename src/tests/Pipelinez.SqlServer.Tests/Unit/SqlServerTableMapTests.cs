using Dapper;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.Internal;
using Pipelinez.SqlServer.Mapping;
using Pipelinez.SqlServer.Tests.EndToEnd.Models;
using Xunit;

namespace Pipelinez.SqlServer.Tests.Unit;

public sealed class SqlServerTableMapTests
{
    [Fact]
    public void SqlServerTableMap_Generated_Command_Uses_Bracket_Quoted_Identifiers_And_Parameters()
    {
        var tableMap = SqlServerTableMap<TestSqlServerRecord>.ForTable("app data", "processed-orders")
            .Map("order id", record => record.Id)
            .MapJson("payload", record => record);

        var command = SqlServerMappedCommandFactory.CreateCommand(
            tableMap,
            new TestSqlServerRecord
            {
                Id = "A-100",
                Value = "good"
            },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
            defaultCommandTimeoutSeconds: 30);

        Assert.Equal(
            "INSERT INTO [app data].[processed-orders] ([order id], [payload]) VALUES (@p0, @p1);",
            command.CommandText);

        var parameters = Assert.IsType<DynamicParameters>(command.Parameters);
        Assert.Equal("A-100", parameters.Get<string>("p0"));
        Assert.Contains("\"id\":\"A-100\"", parameters.Get<string>("p1"), StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerDestinationOptions_Validate_Requires_ConnectionString()
    {
        var options = new SqlServerDestinationOptions();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerDestinationOptions_BuildConnectionString_Applies_Callback()
    {
        var options = new SqlServerDestinationOptions
        {
            ConnectionString = "Server=localhost;Database=test;User Id=sa;Password=P@ssw0rd!2026;TrustServerCertificate=True",
            ConfigureConnectionString = builder => builder.ApplicationName = "Pipelinez.Tests"
        };

        var connectionString = options.BuildConnectionString(nameof(SqlServerDestinationOptions));

        Assert.Contains("Application Name=Pipelinez.Tests", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerTableMap_Validate_Requires_At_Least_One_Column()
    {
        var tableMap = SqlServerTableMap<TestSqlServerRecord>.ForTable("app", "orders");

        var exception = Assert.Throws<InvalidOperationException>(() => tableMap.Validate());
        Assert.Contains("at least one column", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlServerTableMap_Generated_Command_Escapes_Closing_Brackets()
    {
        var tableMap = SqlServerTableMap<TestSqlServerRecord>.ForTable("app]data", "processed]orders")
            .Map("order]id", record => record.Id);

        var command = SqlServerMappedCommandFactory.CreateCommand(
            tableMap,
            new TestSqlServerRecord { Id = "A-100", Value = "good" },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
            defaultCommandTimeoutSeconds: null);

        Assert.Equal(
            "INSERT INTO [app]]data].[processed]]orders] ([order]]id]) VALUES (@p0);",
            command.CommandText);
    }
}

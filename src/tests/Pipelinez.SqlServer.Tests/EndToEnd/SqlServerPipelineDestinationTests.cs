using Pipelinez.Core;
using Pipelinez.SqlServer;
using Pipelinez.SqlServer.Mapping;
using Pipelinez.SqlServer.Tests.EndToEnd.Models;
using Pipelinez.SqlServer.Tests.Infrastructure;
using Xunit;

namespace Pipelinez.SqlServer.Tests.EndToEnd;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SqlServerPipelineDestinationTests(SqlServerTestCluster cluster)
{
    [Fact]
    public async Task SqlServerDestination_TableMap_Writes_Record_To_Consumer_Owned_Table()
    {
        var scenarioName = nameof(SqlServerDestination_TableMap_Writes_Record_To_Consumer_Owned_Table);
        var schemaName = SqlServerNameFactory.CreateSchemaName(scenarioName);
        var tableName = SqlServerNameFactory.CreateTableName(scenarioName);

        await cluster.ExecuteAsync($"CREATE SCHEMA [{schemaName}];");
        await cluster.ExecuteAsync($"""
            CREATE TABLE [{schemaName}].[{tableName}] (
                [order_id] nvarchar(128) NOT NULL,
                [payload] nvarchar(max) NOT NULL CHECK (ISJSON([payload]) = 1),
                [processed_at_utc] datetimeoffset NOT NULL
            );
            """);

        var pipeline = Pipeline<TestSqlServerRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .WithSqlServerDestination(
                cluster.CreateDestinationOptions(),
                SqlServerTableMap<TestSqlServerRecord>.ForTable(schemaName, tableName)
                    .Map("order_id", record => record.Id)
                    .MapJson("payload", record => record)
                    .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestSqlServerRecord
        {
            Id = "A-100",
            Value = "good"
        });
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        var row = await cluster.QuerySingleAsync<(string OrderId, string Value)>(
            $"""SELECT [order_id] AS [OrderId], JSON_VALUE([payload], '$.value') AS [Value] FROM [{schemaName}].[{tableName}]""")
            ;

        Assert.Equal("A-100", row.OrderId);
        Assert.Equal("good", row.Value);
    }

    [Fact]
    public async Task SqlServerDestination_Custom_Command_Writes_Record_To_Consumer_Owned_Table()
    {
        var scenarioName = nameof(SqlServerDestination_Custom_Command_Writes_Record_To_Consumer_Owned_Table);
        var schemaName = SqlServerNameFactory.CreateSchemaName(scenarioName);
        var tableName = SqlServerNameFactory.CreateTableName(scenarioName);

        await cluster.ExecuteAsync($"CREATE SCHEMA [{schemaName}];");
        await cluster.ExecuteAsync($"""
            CREATE TABLE [{schemaName}].[{tableName}] (
                [external_id] nvarchar(128) NOT NULL,
                [status_value] nvarchar(128) NOT NULL
            );
            """);

        var pipeline = Pipeline<TestSqlServerRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .WithSqlServerDestination(
                cluster.CreateDestinationOptions(),
                record => new SqlServerCommandDefinition(
                    $"""INSERT INTO [{schemaName}].[{tableName}] ([external_id], [status_value]) VALUES (@external_id, @status_value);""",
                    new
                    {
                        external_id = record.Id,
                        status_value = record.Value
                    }))
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestSqlServerRecord
        {
            Id = "B-200",
            Value = "processed"
        });
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        var row = await cluster.QuerySingleAsync<(string ExternalId, string StatusValue)>(
            $"""SELECT [external_id] AS [ExternalId], [status_value] AS [StatusValue] FROM [{schemaName}].[{tableName}]""")
            ;

        Assert.Equal("B-200", row.ExternalId);
        Assert.Equal("processed", row.StatusValue);
    }
}

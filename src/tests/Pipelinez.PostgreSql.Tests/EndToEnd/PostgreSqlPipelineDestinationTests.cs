using Pipelinez.Core;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Mapping;
using Pipelinez.PostgreSql.Tests.EndToEnd.Models;
using Pipelinez.PostgreSql.Tests.Infrastructure;
using Xunit;

namespace Pipelinez.PostgreSql.Tests.EndToEnd;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class PostgreSqlPipelineDestinationTests(PostgreSqlTestCluster cluster)
{
    [Fact]
    public async Task PostgreSqlDestination_TableMap_Writes_Record_To_Consumer_Owned_Table()
    {
        var scenarioName = nameof(PostgreSqlDestination_TableMap_Writes_Record_To_Consumer_Owned_Table);
        var schemaName = PostgreSqlNameFactory.CreateSchemaName(scenarioName);
        var tableName = PostgreSqlNameFactory.CreateTableName(scenarioName);

        await cluster.ExecuteAsync($"""
            CREATE SCHEMA "{schemaName}";
            CREATE TABLE "{schemaName}"."{tableName}" (
                "order_id" text NOT NULL,
                "payload" jsonb NOT NULL,
                "processed_at_utc" timestamptz NOT NULL
            );
            """).ConfigureAwait(false);

        var pipeline = Pipeline<TestPostgreSqlRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .WithPostgreSqlDestination(
                cluster.CreateDestinationOptions(),
                PostgreSqlTableMap<TestPostgreSqlRecord>.ForTable(schemaName, tableName)
                    .Map("order_id", record => record.Id)
                    .MapJson("payload", record => record)
                    .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPostgreSqlRecord
        {
            Id = "A-100",
            Value = "good"
        }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var row = await cluster.QuerySingleAsync<(string OrderId, string Value)>(
            $"""SELECT "order_id" AS "OrderId", "payload"->>'value' AS "Value" FROM "{schemaName}"."{tableName}" """)
            .ConfigureAwait(false);

        Assert.Equal("A-100", row.OrderId);
        Assert.Equal("good", row.Value);
    }

    [Fact]
    public async Task PostgreSqlDestination_Custom_Command_Writes_Record_To_Consumer_Owned_Table()
    {
        var scenarioName = nameof(PostgreSqlDestination_Custom_Command_Writes_Record_To_Consumer_Owned_Table);
        var schemaName = PostgreSqlNameFactory.CreateSchemaName(scenarioName);
        var tableName = PostgreSqlNameFactory.CreateTableName(scenarioName);

        await cluster.ExecuteAsync($"""
            CREATE SCHEMA "{schemaName}";
            CREATE TABLE "{schemaName}"."{tableName}" (
                "external_id" text NOT NULL,
                "status_value" text NOT NULL
            );
            """).ConfigureAwait(false);

        var pipeline = Pipeline<TestPostgreSqlRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .WithPostgreSqlDestination(
                cluster.CreateDestinationOptions(),
                record => new PostgreSqlCommandDefinition(
                    $"""INSERT INTO "{schemaName}"."{tableName}" ("external_id", "status_value") VALUES (@external_id, @status_value)""",
                    new
                    {
                        external_id = record.Id,
                        status_value = record.Value
                    }))
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPostgreSqlRecord
        {
            Id = "B-200",
            Value = "processed"
        }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var row = await cluster.QuerySingleAsync<(string ExternalId, string StatusValue)>(
            $"""SELECT "external_id" AS "ExternalId", "status_value" AS "StatusValue" FROM "{schemaName}"."{tableName}" """)
            .ConfigureAwait(false);

        Assert.Equal("B-200", row.ExternalId);
        Assert.Equal("processed", row.StatusValue);
    }
}

using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Mapping;
using Pipelinez.PostgreSql.Tests.EndToEnd.Models;
using Pipelinez.PostgreSql.Tests.Infrastructure;
using Xunit;

namespace Pipelinez.PostgreSql.Tests.EndToEnd;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class PostgreSqlPipelineDeadLetterTests(PostgreSqlTestCluster cluster)
{
    [Fact]
    public async Task PostgreSqlDeadLetterDestination_TableMap_Writes_Faulted_Record_And_Allows_Later_Records_To_Continue()
    {
        var scenarioName = nameof(PostgreSqlDeadLetterDestination_TableMap_Writes_Faulted_Record_And_Allows_Later_Records_To_Continue);
        var schemaName = PostgreSqlNameFactory.CreateSchemaName(scenarioName);
        var tableName = PostgreSqlNameFactory.CreateTableName(scenarioName);
        var destination = new CollectingDestination();

        await cluster.ExecuteAsync($"""
            CREATE SCHEMA "{schemaName}";
            CREATE TABLE "{schemaName}"."{tableName}" (
                "order_id" text NOT NULL,
                "fault_component" text NOT NULL,
                "record_json" jsonb NOT NULL,
                "metadata_json" jsonb NOT NULL,
                "dead_lettered_at_utc" timestamptz NOT NULL
            );
            """).ConfigureAwait(false);

        var pipeline = Pipeline<TestPostgreSqlRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingPostgreSqlSegment("bad"), new object())
            .WithDestination(destination)
            .WithPostgreSqlDeadLetterDestination(
                cluster.CreateDeadLetterOptions(),
                PostgreSqlTableMap<PipelineDeadLetterRecord<TestPostgreSqlRecord>>.ForTable(schemaName, tableName)
                    .Map("order_id", deadLetter => deadLetter.Record.Id)
                    .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
                    .MapJson("record_json", deadLetter => deadLetter.Record)
                    .MapJson("metadata_json", deadLetter => deadLetter.Metadata)
                    .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPostgreSqlRecord { Id = "bad-1", Value = "bad" }).ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPostgreSqlRecord { Id = "good-1", Value = "good" }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var completed = Assert.Single(destination.Records);
        Assert.Equal("good-1", completed.Id);
        Assert.Equal("good|processed", completed.Value);

        var row = await cluster.QuerySingleAsync<(string OrderId, string FaultComponent, string Value)>(
            $"""SELECT "order_id" AS "OrderId", "fault_component" AS "FaultComponent", "record_json"->>'value' AS "Value" FROM "{schemaName}"."{tableName}" """)
            .ConfigureAwait(false);

        Assert.Equal("bad-1", row.OrderId);
        Assert.Equal(nameof(ConditionalFaultingPostgreSqlSegment), row.FaultComponent);
        Assert.Equal("bad", row.Value);
    }

    [Fact]
    public async Task PostgreSqlDeadLetterDestination_Custom_Command_Writes_Faulted_Record()
    {
        var scenarioName = nameof(PostgreSqlDeadLetterDestination_Custom_Command_Writes_Faulted_Record);
        var schemaName = PostgreSqlNameFactory.CreateSchemaName(scenarioName);
        var tableName = PostgreSqlNameFactory.CreateTableName(scenarioName);

        await cluster.ExecuteAsync($"""
            CREATE SCHEMA "{schemaName}";
            CREATE TABLE "{schemaName}"."{tableName}" (
                "record_id" text NOT NULL,
                "fault_message" text NOT NULL
            );
            """).ConfigureAwait(false);

        var pipeline = Pipeline<TestPostgreSqlRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingPostgreSqlSegment("bad"), new object())
            .WithInMemoryDestination("ignored")
            .WithPostgreSqlDeadLetterDestination(
                cluster.CreateDeadLetterOptions(),
                deadLetter => new PostgreSqlCommandDefinition(
                    $"""INSERT INTO "{schemaName}"."{tableName}" ("record_id", "fault_message") VALUES (@record_id, @fault_message)""",
                    new
                    {
                        record_id = deadLetter.Record.Id,
                        fault_message = deadLetter.Fault.Message
                    }))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPostgreSqlRecord { Id = "bad-2", Value = "bad" }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var row = await cluster.QuerySingleAsync<(string RecordId, string FaultMessage)>(
            $"""SELECT "record_id" AS "RecordId", "fault_message" AS "FaultMessage" FROM "{schemaName}"."{tableName}" """)
            .ConfigureAwait(false);

        Assert.Equal("bad-2", row.RecordId);
        Assert.Contains("configured to fail", row.FaultMessage, StringComparison.OrdinalIgnoreCase);
    }
}

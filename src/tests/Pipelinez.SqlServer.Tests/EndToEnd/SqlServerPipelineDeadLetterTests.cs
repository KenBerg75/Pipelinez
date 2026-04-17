using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.SqlServer;
using Pipelinez.SqlServer.Mapping;
using Pipelinez.SqlServer.Tests.EndToEnd.Models;
using Pipelinez.SqlServer.Tests.Infrastructure;
using Xunit;

namespace Pipelinez.SqlServer.Tests.EndToEnd;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SqlServerPipelineDeadLetterTests(SqlServerTestCluster cluster)
{
    [Fact]
    public async Task SqlServerDeadLetterDestination_TableMap_Writes_Faulted_Record_And_Allows_Later_Records_To_Continue()
    {
        var scenarioName = nameof(SqlServerDeadLetterDestination_TableMap_Writes_Faulted_Record_And_Allows_Later_Records_To_Continue);
        var schemaName = SqlServerNameFactory.CreateSchemaName(scenarioName);
        var tableName = SqlServerNameFactory.CreateTableName(scenarioName);
        var destination = new CollectingDestination();

        await cluster.ExecuteAsync($"CREATE SCHEMA [{schemaName}];");
        await cluster.ExecuteAsync($"""
            CREATE TABLE [{schemaName}].[{tableName}] (
                [order_id] nvarchar(128) NOT NULL,
                [fault_component] nvarchar(256) NOT NULL,
                [record_json] nvarchar(max) NOT NULL CHECK (ISJSON([record_json]) = 1),
                [metadata_json] nvarchar(max) NOT NULL CHECK (ISJSON([metadata_json]) = 1),
                [dead_lettered_at_utc] datetimeoffset NOT NULL
            );
            """);

        var pipeline = Pipeline<TestSqlServerRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingSqlServerSegment("bad"), new object())
            .WithDestination(destination)
            .WithSqlServerDeadLetterDestination(
                cluster.CreateDeadLetterOptions(),
                SqlServerTableMap<PipelineDeadLetterRecord<TestSqlServerRecord>>.ForTable(schemaName, tableName)
                    .Map("order_id", deadLetter => deadLetter.Record.Id)
                    .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
                    .MapJson("record_json", deadLetter => deadLetter.Record)
                    .MapJson("metadata_json", deadLetter => deadLetter.Metadata)
                    .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestSqlServerRecord { Id = "bad-1", Value = "bad" });
        await pipeline.PublishAsync(new TestSqlServerRecord { Id = "good-1", Value = "good" });
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        var completed = Assert.Single(destination.Records);
        Assert.Equal("good-1", completed.Id);
        Assert.Equal("good|processed", completed.Value);

        var row = await cluster.QuerySingleAsync<(string OrderId, string FaultComponent, string Value)>(
            $"""SELECT [order_id] AS [OrderId], [fault_component] AS [FaultComponent], JSON_VALUE([record_json], '$.value') AS [Value] FROM [{schemaName}].[{tableName}]""")
            ;

        Assert.Equal("bad-1", row.OrderId);
        Assert.Equal(nameof(ConditionalFaultingSqlServerSegment), row.FaultComponent);
        Assert.Equal("bad", row.Value);
    }

    [Fact]
    public async Task SqlServerDeadLetterDestination_Custom_Command_Writes_Faulted_Record()
    {
        var scenarioName = nameof(SqlServerDeadLetterDestination_Custom_Command_Writes_Faulted_Record);
        var schemaName = SqlServerNameFactory.CreateSchemaName(scenarioName);
        var tableName = SqlServerNameFactory.CreateTableName(scenarioName);

        await cluster.ExecuteAsync($"CREATE SCHEMA [{schemaName}];");
        await cluster.ExecuteAsync($"""
            CREATE TABLE [{schemaName}].[{tableName}] (
                [record_id] nvarchar(128) NOT NULL,
                [fault_message] nvarchar(max) NOT NULL
            );
            """);

        var pipeline = Pipeline<TestSqlServerRecord>.New(scenarioName)
            .WithInMemorySource(new object())
            .AddSegment(new ConditionalFaultingSqlServerSegment("bad"), new object())
            .WithInMemoryDestination("ignored")
            .WithSqlServerDeadLetterDestination(
                cluster.CreateDeadLetterOptions(),
                deadLetter => new SqlServerCommandDefinition(
                    $"""INSERT INTO [{schemaName}].[{tableName}] ([record_id], [fault_message]) VALUES (@record_id, @fault_message);""",
                    new
                    {
                        record_id = deadLetter.Record.Id,
                        fault_message = deadLetter.Fault.Message
                    }))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestSqlServerRecord { Id = "bad-2", Value = "bad" });
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        var row = await cluster.QuerySingleAsync<(string RecordId, string FaultMessage)>(
            $"""SELECT [record_id] AS [RecordId], [fault_message] AS [FaultMessage] FROM [{schemaName}].[{tableName}]""")
            ;

        Assert.Equal("bad-2", row.RecordId);
        Assert.Contains("configured to fail", row.FaultMessage, StringComparison.OrdinalIgnoreCase);
    }
}

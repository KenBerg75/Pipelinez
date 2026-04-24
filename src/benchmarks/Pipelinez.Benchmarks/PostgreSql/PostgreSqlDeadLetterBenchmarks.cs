using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class PostgreSqlDeadLetterBenchmarks : BenchmarkSuiteBase
{
    private PostgreSqlBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _faultingRecords = [];
    private string? _schemaName;
    private string? _tableName;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _cluster = new PostgreSqlBenchmarkCluster();
        await _cluster.InitializeAsync().ConfigureAwait(false);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _faultingRecords = CreateFaultingRecords();
        _schemaName = BenchmarkScenarioNameFactory.CreateSchemaName(nameof(PostgreSqlDeadLetterBenchmarks), "dead_letter");
        _tableName = BenchmarkScenarioNameFactory.CreateTableName(nameof(PostgreSqlDeadLetterBenchmarks), "records");

        await _cluster!.ExecuteAsync(
            $"""
             CREATE SCHEMA "{_schemaName}";
             CREATE TABLE "{_schemaName}"."{_tableName}" (
                 "record_id" text NOT NULL,
                 "fault_component" text NOT NULL,
                 "record_json" jsonb NOT NULL,
                 "metadata_json" jsonb NOT NULL,
                 "dead_lettered_at_utc" timestamptz NOT NULL
             );
             """).ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_schemaName is not null)
        {
            await _cluster!.ExecuteAsync($"""DROP SCHEMA IF EXISTS "{_schemaName}" CASCADE;""").ConfigureAwait(false);
        }
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (_cluster is not null)
        {
            await _cluster.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public async Task InMemorySourceToPostgreSqlDeadLetterDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-postgresql-dead-letter")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .WithPostgreSqlDeadLetterDestination(
                _cluster!.CreateDeadLetterOptions(),
                PostgreSqlTableMap<PipelineDeadLetterRecord<BenchmarkRecord>>.ForTable(_schemaName!, _tableName!)
                    .Map("record_id", deadLetter => deadLetter.Record.Id)
                    .Map("fault_component", deadLetter => deadLetter.Fault.ComponentName)
                    .MapJson("record_json", deadLetter => deadLetter.Record)
                    .MapJson("metadata_json", deadLetter => deadLetter.Metadata)
                    .Map("dead_lettered_at_utc", deadLetter => deadLetter.DeadLetteredAtUtc))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _faultingRecords).ConfigureAwait(false);
        await tracker.WaitForDeadLetteredAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForRowCountAsync($""""{_schemaName}"."{_tableName}"""", RecordCount, ObservationTimeout).ConfigureAwait(false);

        var rowCount = await _cluster.QuerySingleAsync<long>($"""SELECT COUNT(*) FROM "{_schemaName}"."{_tableName}" """).ConfigureAwait(false);
        if (rowCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} PostgreSQL dead-letter rows but observed {rowCount}.");
        }
    }
}

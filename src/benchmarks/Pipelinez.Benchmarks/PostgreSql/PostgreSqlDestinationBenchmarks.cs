using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.PostgreSql;
using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class PostgreSqlDestinationBenchmarks : BenchmarkSuiteBase
{
    private PostgreSqlBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
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
        _records = CreateSuccessfulRecords();
        _schemaName = BenchmarkScenarioNameFactory.CreateSchemaName(nameof(PostgreSqlDestinationBenchmarks), "destination");
        _tableName = BenchmarkScenarioNameFactory.CreateTableName(nameof(PostgreSqlDestinationBenchmarks), "records");

        await _cluster!.ExecuteAsync(
            $"""
             CREATE SCHEMA "{_schemaName}";
             CREATE TABLE "{_schemaName}"."{_tableName}" (
                 "record_id" text NOT NULL,
                 "payload" jsonb NOT NULL,
                 "processed_at_utc" timestamptz NOT NULL
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
    public async Task InMemorySourceToPostgreSqlDestination()
    {
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-postgresql-destination")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithPostgreSqlDestination(
                _cluster!.CreateDestinationOptions(),
                PostgreSqlTableMap<BenchmarkRecord>.ForTable(_schemaName!, _tableName!)
                    .Map("record_id", record => record.Id)
                    .MapJson("payload", record => record)
                    .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
            .Build();

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _records).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForRowCountAsync($""""{_schemaName}"."{_tableName}"""", RecordCount, ObservationTimeout).ConfigureAwait(false);

        var rowCount = await _cluster.QuerySingleAsync<long>($"""SELECT COUNT(*) FROM "{_schemaName}"."{_tableName}" """).ConfigureAwait(false);
        if (rowCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} PostgreSQL destination rows but observed {rowCount}.");
        }
    }
}

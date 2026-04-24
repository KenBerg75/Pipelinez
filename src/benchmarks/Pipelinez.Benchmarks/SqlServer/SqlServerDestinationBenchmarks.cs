using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.SqlServer;
using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class SqlServerDestinationBenchmarks : BenchmarkSuiteBase
{
    private SqlServerBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _schemaName;
    private string? _tableName;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _cluster = new SqlServerBenchmarkCluster();
        await _cluster.InitializeAsync().ConfigureAwait(false);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _records = CreateSuccessfulRecords();
        _schemaName = BenchmarkScenarioNameFactory.CreateSchemaName(nameof(SqlServerDestinationBenchmarks), "destination");
        _tableName = BenchmarkScenarioNameFactory.CreateTableName(nameof(SqlServerDestinationBenchmarks), "records");

        await _cluster!.ExecuteAsync($"""CREATE SCHEMA [{_schemaName}];""").ConfigureAwait(false);
        await _cluster.ExecuteAsync(
            $"""
             CREATE TABLE [{_schemaName}].[{_tableName}] (
                 [record_id] nvarchar(128) NOT NULL,
                 [payload] nvarchar(max) NOT NULL CHECK (ISJSON([payload]) = 1),
                 [processed_at_utc] datetimeoffset NOT NULL
             );
             """).ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_schemaName is not null && _tableName is not null)
        {
            await _cluster!.ExecuteAsync($"""DROP TABLE IF EXISTS [{_schemaName}].[{_tableName}]; DROP SCHEMA IF EXISTS [{_schemaName}];""").ConfigureAwait(false);
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
    public async Task InMemorySourceToSqlServerDestination()
    {
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-sqlserver-destination")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithSqlServerDestination(
                _cluster!.CreateDestinationOptions(),
                SqlServerTableMap<BenchmarkRecord>.ForTable(_schemaName!, _tableName!)
                    .Map("record_id", record => record.Id)
                    .MapJson("payload", record => record)
                    .Map("processed_at_utc", _ => DateTimeOffset.UtcNow))
            .Build();

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _records).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForRowCountAsync($"[{_schemaName}].[{_tableName}]", RecordCount, ObservationTimeout).ConfigureAwait(false);

        var rowCount = await _cluster.QuerySingleAsync<long>($"""SELECT COUNT(*) FROM [{_schemaName}].[{_tableName}]""").ConfigureAwait(false);
        if (rowCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} SQL Server destination rows but observed {rowCount}.");
        }
    }
}

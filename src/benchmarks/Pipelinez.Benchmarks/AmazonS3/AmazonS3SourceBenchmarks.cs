using System.Text;
using BenchmarkDotNet.Attributes;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Source;
using Pipelinez.Core;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class AmazonS3SourceBenchmarks : BenchmarkSuiteBase
{
    private AmazonS3BenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _bucketName;
    private string? _inputPrefix;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _cluster = new AmazonS3BenchmarkCluster();
        await _cluster.InitializeAsync().ConfigureAwait(false);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _records = CreateSuccessfulRecords();
        _bucketName = BenchmarkScenarioNameFactory.CreateBucketName(nameof(AmazonS3SourceBenchmarks), "source");
        _inputPrefix = BenchmarkScenarioNameFactory.CreateObjectPrefix(nameof(AmazonS3SourceBenchmarks), "incoming");

        await _cluster!.CreateBucketAsync(_bucketName).ConfigureAwait(false);

        foreach (var record in _records)
        {
            await _cluster
                .PutObjectAsync(_bucketName, $"{_inputPrefix}{record.Id}.json", $"{record.Id}|{record.Payload}")
                .ConfigureAwait(false);
        }
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_bucketName is not null)
        {
            await _cluster!.DeleteBucketAsync(_bucketName).ConfigureAwait(false);
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
    public async Task ObjectSourceToInMemoryDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-s3-source")
            .WithAmazonS3Source(
                new AmazonS3SourceOptions
                {
                    Connection = _cluster!.CreateConnectionOptions(),
                    Bucket = new AmazonS3BucketOptions
                    {
                        BucketName = _bucketName!,
                        Prefix = _inputPrefix!
                    },
                    Settlement = new AmazonS3ObjectSettlementOptions
                    {
                        OnSuccess = AmazonS3ObjectSettlementAction.Leave
                    }
                },
                MapRecordAsync)
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await tracker.WaitForCompletedAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);

        if (tracker.CompletedCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} completed records but observed {tracker.CompletedCount}.");
        }
    }

    private static async Task<BenchmarkRecord> MapRecordAsync(AmazonS3ObjectContext context, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Content, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var parts = content.Split('|', 2);
        return new BenchmarkRecord
        {
            Id = parts[0],
            Payload = parts[1]
        };
    }
}

using BenchmarkDotNet.Attributes;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.Core;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class AmazonS3DestinationBenchmarks : BenchmarkSuiteBase
{
    private AmazonS3BenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _bucketName;
    private string? _outputPrefix;
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
        _bucketName = BenchmarkScenarioNameFactory.CreateBucketName(nameof(AmazonS3DestinationBenchmarks), "destination");
        _outputPrefix = BenchmarkScenarioNameFactory.CreateObjectPrefix(nameof(AmazonS3DestinationBenchmarks), "processed");

        await _cluster!.CreateBucketAsync(_bucketName).ConfigureAwait(false);
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
    public async Task InMemorySourceToObjectDestination()
    {
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-s3-destination")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithAmazonS3Destination(
                new AmazonS3DestinationOptions
                {
                    Connection = _cluster!.CreateConnectionOptions(),
                    BucketName = _bucketName!,
                    Write = new AmazonS3ObjectWriteOptions
                    {
                        KeyPrefix = _outputPrefix!
                    }
                },
                record => AmazonS3PutObject.FromText($"{record.Id}.json", record.Payload, "application/json"))
            .Build();

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _records).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForObjectCountAsync(_bucketName!, _outputPrefix!, RecordCount, ObservationTimeout).ConfigureAwait(false);

        var objectKeys = await _cluster.ListObjectKeysAsync(_bucketName!, _outputPrefix!).ConfigureAwait(false);
        if (objectKeys.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} Amazon S3 destination objects but observed {objectKeys.Count}.");
        }
    }
}

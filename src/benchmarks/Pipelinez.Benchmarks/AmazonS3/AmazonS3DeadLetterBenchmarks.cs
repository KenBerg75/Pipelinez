using BenchmarkDotNet.Attributes;
using Pipelinez.AmazonS3;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class AmazonS3DeadLetterBenchmarks : BenchmarkSuiteBase
{
    private AmazonS3BenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _faultingRecords = [];
    private string? _bucketName;
    private string? _deadLetterPrefix;
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
        _faultingRecords = CreateFaultingRecords();
        _bucketName = BenchmarkScenarioNameFactory.CreateBucketName(nameof(AmazonS3DeadLetterBenchmarks), "dead-letter");
        _deadLetterPrefix = BenchmarkScenarioNameFactory.CreateObjectPrefix(nameof(AmazonS3DeadLetterBenchmarks), "dead-letter");

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
    public async Task InMemorySourceToS3DeadLetterDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-s3-dead-letter")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .WithAmazonS3DeadLetterDestination(
                new AmazonS3DeadLetterOptions
                {
                    Connection = _cluster!.CreateConnectionOptions(),
                    BucketName = _bucketName!,
                    KeyPrefix = _deadLetterPrefix!
                })
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _faultingRecords).ConfigureAwait(false);
        await tracker.WaitForDeadLetteredAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForObjectCountAsync(_bucketName!, _deadLetterPrefix!, RecordCount, ObservationTimeout).ConfigureAwait(false);

        var objectKeys = await _cluster.ListObjectKeysAsync(_bucketName!, _deadLetterPrefix!).ConfigureAwait(false);
        if (objectKeys.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} Amazon S3 dead-letter objects but observed {objectKeys.Count}.");
        }
    }
}

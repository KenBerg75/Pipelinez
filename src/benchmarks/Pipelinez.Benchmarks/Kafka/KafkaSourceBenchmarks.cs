using BenchmarkDotNet.Attributes;
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Kafka;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class KafkaSourceBenchmarks : BenchmarkSuiteBase
{
    private KafkaBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _sourceTopic;
    private string? _consumerGroup;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _cluster = new KafkaBenchmarkCluster();
        await _cluster.InitializeAsync().ConfigureAwait(false);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _records = CreateSuccessfulRecords();
        _sourceTopic = await _cluster!.CreateTopicAsync(nameof(KafkaSourceBenchmarks), "source").ConfigureAwait(false);
        _consumerGroup = BenchmarkScenarioNameFactory.CreateKafkaConsumerGroupName(nameof(KafkaSourceBenchmarks), "source");

        await _cluster
            .ProduceAsync(
                _sourceTopic,
                _records.Select(record => new Message<string, string>
                {
                    Key = record.Id,
                    Value = record.Payload
                }))
            .ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_sourceTopic is not null)
        {
            await _cluster!.DeleteTopicAsync(_sourceTopic).ConfigureAwait(false);
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
    public async Task SourceToInMemoryDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-kafka-source")
            .WithKafkaSource(
                _cluster!.CreateSourceOptions(_sourceTopic!, _consumerGroup!),
                (string key, string value) => new BenchmarkRecord
                {
                    Id = key,
                    Payload = value
                })
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
}

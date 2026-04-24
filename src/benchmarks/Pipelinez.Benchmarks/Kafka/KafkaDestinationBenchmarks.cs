using BenchmarkDotNet.Attributes;
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Kafka;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class KafkaDestinationBenchmarks : BenchmarkSuiteBase
{
    private KafkaBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _destinationTopic;
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
        _destinationTopic = await _cluster!.CreateTopicAsync(nameof(KafkaDestinationBenchmarks), "destination").ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_destinationTopic is not null)
        {
            await _cluster!.DeleteTopicAsync(_destinationTopic).ConfigureAwait(false);
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
    public async Task InMemorySourceToKafkaDestination()
    {
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-kafka-destination")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithKafkaDestination(
                _cluster!.CreateDestinationOptions(_destinationTopic!),
                record => new Message<string, string>
                {
                    Key = record.Id,
                    Value = record.Payload
                })
            .Build();

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _records).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);

        var consumedMessages = await _cluster
            .ConsumeAsync(
                _destinationTopic!,
                RecordCount,
                BenchmarkScenarioNameFactory.CreateKafkaConsumerGroupName(nameof(KafkaDestinationBenchmarks), "probe"))
            .ConfigureAwait(false);

        if (consumedMessages.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} Kafka destination messages but observed {consumedMessages.Count}.");
        }
    }
}

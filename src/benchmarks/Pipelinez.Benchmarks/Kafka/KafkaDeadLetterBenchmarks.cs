using BenchmarkDotNet.Attributes;
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Kafka;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class KafkaDeadLetterBenchmarks : BenchmarkSuiteBase
{
    private KafkaBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _faultingRecords = [];
    private string? _deadLetterTopic;
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
        _faultingRecords = CreateFaultingRecords();
        _deadLetterTopic = await _cluster!.CreateTopicAsync(nameof(KafkaDeadLetterBenchmarks), "dead-letter").ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_deadLetterTopic is not null)
        {
            await _cluster!.DeleteTopicAsync(_deadLetterTopic).ConfigureAwait(false);
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
    public async Task InMemorySourceToKafkaDeadLetterDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-kafka-dead-letter")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .WithKafkaDeadLetterDestination(
                _cluster!.CreateDestinationOptions(_deadLetterTopic!),
                deadLetter => CreateDeadLetterMessage(deadLetter))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _faultingRecords).ConfigureAwait(false);
        await tracker.WaitForDeadLetteredAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);

        var consumedMessages = await _cluster
            .ConsumeAsync(
                _deadLetterTopic!,
                RecordCount,
                BenchmarkScenarioNameFactory.CreateKafkaConsumerGroupName(nameof(KafkaDeadLetterBenchmarks), "probe"))
            .ConfigureAwait(false);

        if (consumedMessages.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} Kafka dead-letter messages but observed {consumedMessages.Count}.");
        }
    }

    private static Message<string, string> CreateDeadLetterMessage(PipelineDeadLetterRecord<BenchmarkRecord> deadLetter)
    {
        return new Message<string, string>
        {
            Key = deadLetter.Record.Id,
            Value = deadLetter.Record.Payload
        };
    }
}

using System.Text;
using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class RabbitMqDeadLetterBenchmarks : BenchmarkSuiteBase
{
    private RabbitMqBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _faultingRecords = [];
    private string? _deadLetterQueue;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _cluster = new RabbitMqBenchmarkCluster();
        await _cluster.InitializeAsync().ConfigureAwait(false);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _faultingRecords = CreateFaultingRecords();
        _deadLetterQueue = BenchmarkScenarioNameFactory.CreateQueueName(nameof(RabbitMqDeadLetterBenchmarks), "dead-letter");
        await _cluster!.DeclareQueueAsync(_deadLetterQueue).ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_deadLetterQueue is not null)
        {
            await _cluster!.DeleteQueueIfExistsAsync(_deadLetterQueue).ConfigureAwait(false);
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
    public async Task InMemorySourceToRabbitMqDeadLetterDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-rabbitmq-dead-letter")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .WithRabbitMqDeadLetterDestination(
                new RabbitMqDeadLetterOptions
                {
                    Connection = _cluster!.CreateConnectionOptions(),
                    RoutingKey = _deadLetterQueue!
                },
                deadLetter => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes($"{deadLetter.Record.Id}|{deadLetter.Record.Payload}")))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _faultingRecords).ConfigureAwait(false);
        await tracker.WaitForDeadLetteredAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForMessageCountAsync(_deadLetterQueue!, (uint)RecordCount, ObservationTimeout).ConfigureAwait(false);

        var drainedMessages = await _cluster.DrainQueueAsync(_deadLetterQueue!).ConfigureAwait(false);
        if (drainedMessages.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} RabbitMQ dead-letter messages but observed {drainedMessages.Count}.");
        }
    }
}

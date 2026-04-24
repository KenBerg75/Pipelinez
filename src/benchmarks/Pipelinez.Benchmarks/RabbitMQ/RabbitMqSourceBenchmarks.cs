using System.Text;
using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class RabbitMqSourceBenchmarks : BenchmarkSuiteBase
{
    private RabbitMqBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _sourceQueue;
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
        _records = CreateSuccessfulRecords();
        _sourceQueue = BenchmarkScenarioNameFactory.CreateQueueName(nameof(RabbitMqSourceBenchmarks), "source");
        await _cluster!.DeclareQueueAsync(_sourceQueue).ConfigureAwait(false);
        await _cluster
            .PublishMessagesToQueueAsync(_sourceQueue, _records.Select(record => $"{record.Id}|{record.Payload}"))
            .ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_sourceQueue is not null)
        {
            await _cluster!.DeleteQueueIfExistsAsync(_sourceQueue).ConfigureAwait(false);
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
    public async Task QueueSourceToInMemoryDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-rabbitmq-source")
            .WithRabbitMqSource(
                new RabbitMqSourceOptions
                {
                    Connection = _cluster!.CreateConnectionOptions(),
                    Queue = RabbitMqQueueOptions.Named(_sourceQueue!),
                    PrefetchCount = 32
                },
                delivery =>
                {
                    var content = Encoding.UTF8.GetString(delivery.Body.Span);
                    var parts = content.Split('|', 2);
                    return new BenchmarkRecord
                    {
                        Id = parts[0],
                        Payload = parts[1]
                    };
                })
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await tracker.WaitForCompletedAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForQueueToDrainAsync(_sourceQueue!, ObservationTimeout).ConfigureAwait(false);

        if (tracker.CompletedCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} completed records but observed {tracker.CompletedCount}.");
        }
    }
}

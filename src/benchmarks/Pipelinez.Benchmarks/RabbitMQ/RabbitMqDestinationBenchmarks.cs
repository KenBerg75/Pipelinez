using System.Text;
using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class RabbitMqDestinationBenchmarks : BenchmarkSuiteBase
{
    private RabbitMqBenchmarkCluster? _cluster;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _destinationQueue;
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
        _destinationQueue = BenchmarkScenarioNameFactory.CreateQueueName(nameof(RabbitMqDestinationBenchmarks), "destination");
        await _cluster!.DeclareQueueAsync(_destinationQueue).ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_destinationQueue is not null)
        {
            await _cluster!.DeleteQueueIfExistsAsync(_destinationQueue).ConfigureAwait(false);
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
    public async Task InMemorySourceToQueueDestination()
    {
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-rabbitmq-destination")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithRabbitMqDestination(
                new RabbitMqDestinationOptions
                {
                    Connection = _cluster!.CreateConnectionOptions(),
                    RoutingKey = _destinationQueue!
                },
                record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes($"{record.Id}|{record.Payload}")))
            .Build();

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _records).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);
        await _cluster!.WaitForMessageCountAsync(_destinationQueue!, (uint)RecordCount, ObservationTimeout).ConfigureAwait(false);

        var drainedMessages = await _cluster.DrainQueueAsync(_destinationQueue!).ConfigureAwait(false);
        if (drainedMessages.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} RabbitMQ destination messages but observed {drainedMessages.Count}.");
        }
    }
}

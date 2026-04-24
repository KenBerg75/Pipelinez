using Azure.Messaging.ServiceBus;
using BenchmarkDotNet.Attributes;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class AzureServiceBusSourceBenchmarks : BenchmarkSuiteBase
{
    private AzureServiceBusBenchmarkNamespace? _namespace;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _sourceQueue;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _namespace = new AzureServiceBusBenchmarkNamespace(BenchmarkEnvironment.AzureServiceBusConnectionString!);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _records = CreateSuccessfulRecords();
        _sourceQueue = BenchmarkScenarioNameFactory.CreateQueueName(nameof(AzureServiceBusSourceBenchmarks), "source");

        await _namespace!.CreateQueueAsync(_sourceQueue).ConfigureAwait(false);
        await _namespace
            .SendMessagesAsync(
                _sourceQueue,
                _records.Select(record => new ServiceBusMessage(BinaryData.FromString(record.Payload))
                {
                    MessageId = record.Id
                }))
            .ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_sourceQueue is not null)
        {
            await _namespace!.DeleteQueueIfExistsAsync(_sourceQueue).ConfigureAwait(false);
        }
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (_namespace is not null)
        {
            await _namespace.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public async Task QueueSourceToInMemoryDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-asb-source")
            .WithAzureServiceBusSource(
                new AzureServiceBusSourceOptions
                {
                    Connection = _namespace!.CreateConnectionOptions(),
                    Entity = AzureServiceBusEntityOptions.ForQueue(_sourceQueue!),
                    MaxConcurrentCalls = 8
                },
                message => new BenchmarkRecord
                {
                    Id = message.MessageId,
                    Payload = message.Body.ToString()
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

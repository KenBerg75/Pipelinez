using Azure.Messaging.ServiceBus;
using BenchmarkDotNet.Attributes;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class AzureServiceBusDestinationBenchmarks : BenchmarkSuiteBase
{
    private AzureServiceBusBenchmarkNamespace? _namespace;
    private IReadOnlyList<BenchmarkRecord> _records = [];
    private string? _destinationQueue;
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
        _destinationQueue = BenchmarkScenarioNameFactory.CreateQueueName(nameof(AzureServiceBusDestinationBenchmarks), "destination");
        await _namespace!.CreateQueueAsync(_destinationQueue).ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_destinationQueue is not null)
        {
            await _namespace!.DeleteQueueIfExistsAsync(_destinationQueue).ConfigureAwait(false);
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
    public async Task InMemorySourceToQueueDestination()
    {
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-asb-destination")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithAzureServiceBusDestination(
                new AzureServiceBusDestinationOptions
                {
                    Connection = _namespace!.CreateConnectionOptions(),
                    Entity = AzureServiceBusEntityOptions.ForQueue(_destinationQueue!)
                },
                record => new ServiceBusMessage(BinaryData.FromString(record.Payload))
                {
                    MessageId = record.Id
                })
            .Build();

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _records).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);

        var receivedMessages = await _namespace!
            .ReceiveMessagesAsync(_destinationQueue!, RecordCount, ObservationTimeout)
            .ConfigureAwait(false);

        if (receivedMessages.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} Azure Service Bus destination messages but observed {receivedMessages.Count}.");
        }
    }
}

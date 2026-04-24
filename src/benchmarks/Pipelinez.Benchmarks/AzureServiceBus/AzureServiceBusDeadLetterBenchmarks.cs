using Azure.Messaging.ServiceBus;
using BenchmarkDotNet.Attributes;
using Pipelinez.AzureServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class AzureServiceBusDeadLetterBenchmarks : BenchmarkSuiteBase
{
    private AzureServiceBusBenchmarkNamespace? _namespace;
    private IReadOnlyList<BenchmarkRecord> _faultingRecords = [];
    private string? _deadLetterQueue;
    private IPipeline<BenchmarkRecord>? _pipeline;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _namespace = new AzureServiceBusBenchmarkNamespace(BenchmarkEnvironment.AzureServiceBusConnectionString!);
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        _faultingRecords = CreateFaultingRecords();
        _deadLetterQueue = BenchmarkScenarioNameFactory.CreateQueueName(nameof(AzureServiceBusDeadLetterBenchmarks), "dead-letter");
        await _namespace!.CreateQueueAsync(_deadLetterQueue).ConfigureAwait(false);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await BenchmarkPipelineRunner.TryStopAsync(_pipeline).ConfigureAwait(false);
        _pipeline = null;

        if (_deadLetterQueue is not null)
        {
            await _namespace!.DeleteQueueIfExistsAsync(_deadLetterQueue).ConfigureAwait(false);
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
    public async Task InMemorySourceToAzureServiceBusDeadLetterDestination()
    {
        var tracker = new BenchmarkCompletionTracker();
        _pipeline = Pipeline<BenchmarkRecord>.New("benchmark-asb-dead-letter")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .WithAzureServiceBusDeadLetterDestination(
                new AzureServiceBusDeadLetterOptions
                {
                    Connection = _namespace!.CreateConnectionOptions(),
                    Entity = AzureServiceBusEntityOptions.ForQueue(_deadLetterQueue!)
                },
                deadLetter => new ServiceBusMessage(BinaryData.FromString(deadLetter.Record.Payload))
                {
                    MessageId = deadLetter.Record.Id
                })
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        tracker.Attach(_pipeline);

        await _pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(_pipeline, _faultingRecords).ConfigureAwait(false);
        await tracker.WaitForDeadLetteredAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(_pipeline).ConfigureAwait(false);

        var receivedMessages = await _namespace!
            .ReceiveMessagesAsync(_deadLetterQueue!, RecordCount, ObservationTimeout)
            .ConfigureAwait(false);

        if (receivedMessages.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} Azure Service Bus dead-letter messages but observed {receivedMessages.Count}.");
        }
    }
}

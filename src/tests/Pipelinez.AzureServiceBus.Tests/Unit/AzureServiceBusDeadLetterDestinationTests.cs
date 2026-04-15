using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.DeadLettering;
using Pipelinez.AzureServiceBus.Tests.Infrastructure;
using Pipelinez.AzureServiceBus.Tests.Models;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Xunit;

namespace Pipelinez.AzureServiceBus.Tests.Unit;

public class AzureServiceBusDeadLetterDestinationTests
{
    [Fact]
    public async Task DeadLetter_Destination_Sends_Mapped_Message_With_Fault_Metadata()
    {
        var factory = new FakeAzureServiceBusClientFactory();
        var destination = new AzureServiceBusDeadLetterDestination<TestAzureServiceBusRecord>(
            new AzureServiceBusDeadLetterOptions
            {
                Connection = new AzureServiceBusConnectionOptions
                {
                    ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
                },
                Entity = AzureServiceBusEntityOptions.ForQueue("orders-dead-letter")
            },
            deadLetter => new ServiceBusMessage(BinaryData.FromString(deadLetter.Record.Value)),
            factory);

        var record = new TestAzureServiceBusRecord { Id = "42", Value = "bad" };
        record.Headers.Add(new() { Key = "tenant", Value = "south" });

        await destination.WriteAsync(CreateDeadLetterRecord(record), CancellationToken.None);

        var message = Assert.Single(factory.Sender.Messages);
        Assert.Equal("bad", message.Body.ToString());
        Assert.Equal("south", message.ApplicationProperties["tenant"]);
        Assert.Equal("SegmentA", message.ApplicationProperties["pipelinez-deadletter-component"]);
        Assert.Equal("Segment", message.ApplicationProperties["pipelinez-deadletter-kind"]);
        Assert.Equal("AzureServiceBus", message.ApplicationProperties["pipelinez-pipeline-source-transport"]);
        Assert.Equal("asb:Queue:orders", message.ApplicationProperties["pipelinez-pipeline-lease-id"]);
    }

    private static PipelineDeadLetterRecord<TestAzureServiceBusRecord> CreateDeadLetterRecord(
        TestAzureServiceBusRecord record)
    {
        return new PipelineDeadLetterRecord<TestAzureServiceBusRecord>
        {
            Record = record,
            Fault = new PipelineFaultState(
                new InvalidOperationException("boom"),
                "SegmentA",
                PipelineComponentKind.Segment,
                DateTimeOffset.UtcNow,
                "boom"),
            Metadata = new(),
            SegmentHistory = Array.Empty<Pipelinez.Core.FaultHandling.PipelineSegmentExecution>(),
            RetryHistory = Array.Empty<Pipelinez.Core.Retry.PipelineRetryAttempt>(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            DeadLetteredAtUtc = DateTimeOffset.UtcNow,
            Distribution = new PipelineRecordDistributionContext(
                "instance",
                "worker",
                "AzureServiceBus",
                "asb:Queue:orders",
                "orders",
                null,
                10)
        };
    }
}

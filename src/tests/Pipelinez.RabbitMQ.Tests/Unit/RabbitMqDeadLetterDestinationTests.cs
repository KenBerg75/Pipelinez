using System.Text;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Retry;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.DeadLettering;
using Pipelinez.RabbitMQ.Destination;
using Pipelinez.RabbitMQ.Tests.Infrastructure;
using Pipelinez.RabbitMQ.Tests.Models;
using RabbitMQ.Client;
using Xunit;

namespace Pipelinez.RabbitMQ.Tests.Unit;

public class RabbitMqDeadLetterDestinationTests
{
    [Fact]
    public async Task DeadLetterDestination_Publishes_Mapped_Message_With_DeadLetter_Headers()
    {
        var factory = new FakeRabbitMqClientFactory();
        var destination = new RabbitMqDeadLetterDestination<TestRabbitMqRecord>(
            CreateDeadLetterOptions(),
            deadLetter =>
            {
                var properties = new BasicProperties { MessageId = deadLetter.Record.Id };
                return RabbitMqPublishMessage.Create(
                    Encoding.UTF8.GetBytes(deadLetter.Record.Value),
                    properties: properties);
            },
            factory);
        var record = new TestRabbitMqRecord { Id = "1", Value = "bad" };
        record.Headers.Add(new() { Key = "tenant", Value = "south" });

        await destination.WriteAsync(CreateDeadLetterRecord(record), CancellationToken.None);

        var message = Assert.Single(factory.Channel.PublishedMessages);
        Assert.Equal("deadletters", message.Exchange);
        Assert.Equal("pipelinez.dead", message.RoutingKey);
        Assert.True(message.Mandatory);
        Assert.Equal("bad", Encoding.UTF8.GetString(message.Body));
        Assert.Equal("1", message.Properties.MessageId);
        Assert.NotNull(message.Properties.Headers);
        Assert.Equal("south", message.Properties.Headers["tenant"]);
        Assert.Equal("SegmentA", message.Properties.Headers["pipelinez-deadletter-component"]);
        Assert.Equal("Segment", message.Properties.Headers["pipelinez-deadletter-kind"]);
        Assert.Equal("RabbitMQ", message.Properties.Headers["pipelinez-pipeline-source-transport"]);
        Assert.Equal("rabbitmq:queue:orders-in", message.Properties.Headers["pipelinez-pipeline-lease-id"]);
        Assert.Equal("42", message.Properties.Headers["pipelinez-pipeline-offset"]);
    }

    [Fact]
    public async Task DeadLetterDestination_Propagates_Publish_Failure()
    {
        var factory = new FakeRabbitMqClientFactory();
        factory.Channel.PublishException = new InvalidOperationException("publish failed");
        var destination = new RabbitMqDeadLetterDestination<TestRabbitMqRecord>(
            CreateDeadLetterOptions(),
            deadLetter => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(deadLetter.Record.Value)),
            factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => destination.WriteAsync(
                CreateDeadLetterRecord(new TestRabbitMqRecord { Id = "1", Value = "bad" }),
                CancellationToken.None));
    }

    private static RabbitMqDeadLetterOptions CreateDeadLetterOptions()
    {
        return new RabbitMqDeadLetterOptions
        {
            Connection = new RabbitMqConnectionOptions
            {
                Uri = new Uri("amqp://guest:guest@localhost:5672/")
            },
            Exchange = "deadletters",
            RoutingKey = "pipelinez.dead"
        };
    }

    private static PipelineDeadLetterRecord<TestRabbitMqRecord> CreateDeadLetterRecord(
        TestRabbitMqRecord record)
    {
        return new PipelineDeadLetterRecord<TestRabbitMqRecord>
        {
            Record = record,
            Fault = new PipelineFaultState(
                new InvalidOperationException("boom"),
                "SegmentA",
                PipelineComponentKind.Segment,
                DateTimeOffset.UtcNow,
                "boom"),
            Metadata = new MetadataCollection(),
            SegmentHistory = Array.Empty<PipelineSegmentExecution>(),
            RetryHistory = Array.Empty<PipelineRetryAttempt>(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            DeadLetteredAtUtc = DateTimeOffset.UtcNow,
            Distribution = new PipelineRecordDistributionContext(
                "instance",
                "worker",
                "RabbitMQ",
                "rabbitmq:queue:orders-in",
                "orders-in",
                null,
                42)
        };
    }
}

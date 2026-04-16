using System.Text;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Segment;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Source;
using Pipelinez.RabbitMQ.Tests.Infrastructure;
using Pipelinez.RabbitMQ.Tests.Models;
using RabbitMQ.Client;
using Xunit;

namespace Pipelinez.RabbitMQ.Tests.Unit;

public class RabbitMqSourceTests
{
    [Fact]
    public async Task Source_Acks_Delivery_After_Pipeline_Completion()
    {
        var factory = new FakeRabbitMqClientFactory();
        var source = new RabbitMqPipelineSource<TestRabbitMqRecord>(
            CreateSourceOptions(),
            delivery => new TestRabbitMqRecord
            {
                Id = delivery.Properties?.MessageId ?? "missing",
                Value = Encoding.UTF8.GetString(delivery.Body.Span)
            },
            factory);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbit-source")
            .WithSource(source)
            .WithInMemoryDestination("memory")
            .Build();

        await pipeline.StartPipelineAsync();
        await WaitUntilAsync(() => factory.Channel.SourceConfigured);
        await factory.Channel.DeliverAsync(CreateDelivery(1, "hello"));
        await pipeline.CompleteAsync();

        var settlement = Assert.Single(factory.Channel.Settlements);
        Assert.Equal(1UL, settlement.DeliveryTag);
        Assert.Equal("ack", settlement.Action);
    }

    [Fact]
    public async Task Source_Nacks_Without_Requeue_For_Configured_Pipeline_DeadLetter()
    {
        var factory = new FakeRabbitMqClientFactory();
        var sourceOptions = CreateSourceOptions();
        sourceOptions.Settlement.PipelineDeadLetterSettlement =
            RabbitMqPipelineDeadLetterSettlement.DeadLetterOrDiscardSourceMessage;

        var source = new RabbitMqPipelineSource<TestRabbitMqRecord>(
            sourceOptions,
            delivery => new TestRabbitMqRecord
            {
                Id = delivery.Properties?.MessageId ?? "missing",
                Value = Encoding.UTF8.GetString(delivery.Body.Span)
            },
            factory);
        var deadLetters = new InMemoryDeadLetterDestination<TestRabbitMqRecord>();

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbit-source-deadletter")
            .WithSource(source)
            .AddSegment(new FaultingSegment(), new object())
            .WithInMemoryDestination("memory")
            .WithDeadLetterDestination(deadLetters)
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        await WaitUntilAsync(() => factory.Channel.SourceConfigured);
        await factory.Channel.DeliverAsync(CreateDelivery(7, "bad"));
        await pipeline.CompleteAsync();

        var settlement = Assert.Single(factory.Channel.Settlements);
        Assert.Equal(7UL, settlement.DeliveryTag);
        Assert.Equal("nack", settlement.Action);
        Assert.False(settlement.Requeue);
        Assert.Single(deadLetters.Records);
    }

    private static RabbitMqSourceOptions CreateSourceOptions()
    {
        return new RabbitMqSourceOptions
        {
            Connection = new RabbitMqConnectionOptions
            {
                Uri = new Uri("amqp://guest:guest@localhost:5672/")
            },
            Queue = RabbitMqQueueOptions.Named("orders-in")
        };
    }

    private static RabbitMqDeliveryContext CreateDelivery(ulong deliveryTag, string body)
    {
        return new RabbitMqDeliveryContext
        {
            ConsumerTag = "consumer",
            DeliveryTag = deliveryTag,
            Redelivered = false,
            Exchange = "orders",
            RoutingKey = "created",
            Body = Encoding.UTF8.GetBytes(body),
            Properties = new BasicProperties
            {
                MessageId = deliveryTag.ToString()
            }
        };
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(25, timeout.Token);
        }
    }

    private sealed class FaultingSegment : PipelineSegment<TestRabbitMqRecord>
    {
        public override Task<TestRabbitMqRecord> ExecuteAsync(TestRabbitMqRecord arg)
        {
            throw new InvalidOperationException("boom");
        }
    }
}

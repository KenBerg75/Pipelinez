using System.Text;
using DotNet.Testcontainers.Builders;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Segment;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;
using Pipelinez.RabbitMQ.Source;
using Pipelinez.RabbitMQ.Tests.Infrastructure;
using Pipelinez.RabbitMQ.Tests.Models;
using Xunit;

namespace Pipelinez.RabbitMQ.Tests.EndToEnd;

public class RabbitMqPipelineIntegrationTests
{
    [Fact]
    public async Task Destination_Publishes_To_Default_Exchange_Queue()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var queueName = RabbitMqNameFactory.CreateName("destination");
        await cluster.DeclareQueueAsync(queueName);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbitmq-destination-integration")
            .WithInMemorySource(new object())
            .WithRabbitMqDestination(
                new RabbitMqDestinationOptions
                {
                    Connection = CreateConnectionOptions(cluster),
                    RoutingKey = queueName
                },
                record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Value)))
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestRabbitMqRecord { Id = "1", Value = "hello" });
        await pipeline.CompleteAsync();

        var message = await cluster.WaitForMessageAsync(queueName);
        Assert.Equal("hello", Encoding.UTF8.GetString(message.Body.Span));
    }

    [Fact]
    public async Task Source_Consumes_Queue_And_Acks_After_Destination_Succeeds()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var inputQueue = RabbitMqNameFactory.CreateName("source-in");
        var outputQueue = RabbitMqNameFactory.CreateName("source-out");
        await cluster.DeclareQueueAsync(inputQueue);
        await cluster.DeclareQueueAsync(outputQueue);
        await cluster.PublishToQueueAsync(inputQueue, "hello", ("tenant", "north"));

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbitmq-source-integration")
            .WithRabbitMqSource(
                new RabbitMqSourceOptions
                {
                    Connection = CreateConnectionOptions(cluster),
                    Queue = RabbitMqQueueOptions.Named(inputQueue)
                },
                delivery => new TestRabbitMqRecord
                {
                    Id = delivery.DeliveryTag.ToString(),
                    Value = Encoding.UTF8.GetString(delivery.Body.Span)
                })
            .WithRabbitMqDestination(
                new RabbitMqDestinationOptions
                {
                    Connection = CreateConnectionOptions(cluster),
                    RoutingKey = outputQueue
                },
                record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Value)))
            .Build();

        await pipeline.StartPipelineAsync();
        var output = await cluster.WaitForMessageAsync(outputQueue);
        await cluster.WaitForQueueToDrainAsync(inputQueue);
        await pipeline.CompleteAsync();

        Assert.Equal("hello", Encoding.UTF8.GetString(output.Body.Span));
    }

    [Fact]
    public async Task DeadLetterDestination_Publishes_Faulted_Record_To_RabbitMq_Queue()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var deadLetterQueue = RabbitMqNameFactory.CreateName("pipelinez-dead");
        await cluster.DeclareQueueAsync(deadLetterQueue);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbitmq-deadletter-integration")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingSegment(), new object())
            .WithInMemoryDestination("memory")
            .WithRabbitMqDeadLetterDestination(
                new RabbitMqDeadLetterOptions
                {
                    Connection = CreateConnectionOptions(cluster),
                    RoutingKey = deadLetterQueue
                },
                deadLetter => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(deadLetter.Record.Value)))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestRabbitMqRecord { Id = "1", Value = "bad" });
        await pipeline.CompleteAsync();

        var message = await cluster.WaitForMessageAsync(deadLetterQueue);
        Assert.Equal("bad", Encoding.UTF8.GetString(message.Body.Span));
        Assert.NotNull(message.BasicProperties.Headers);
        Assert.Equal("FaultingSegment", FormatHeader(message.BasicProperties.Headers["pipelinez-deadletter-component"]));
    }

    [Fact]
    public async Task Source_DeadLetter_Action_Nacks_To_Configured_Dlx()
    {
        await using var cluster = await TryStartClusterAsync();
        if (cluster is null)
        {
            return;
        }

        var inputQueue = RabbitMqNameFactory.CreateName("source-dlx-in");
        var deadLetterExchange = RabbitMqNameFactory.CreateName("source-dlx-ex");
        var deadLetterQueue = RabbitMqNameFactory.CreateName("source-dlx-out");
        var deadLetterRoutingKey = RabbitMqNameFactory.CreateName("source-dlx-route");

        await cluster.DeclareExchangeAsync(deadLetterExchange);
        await cluster.DeclareQueueAsync(deadLetterQueue);
        await cluster.BindQueueAsync(deadLetterQueue, deadLetterExchange, deadLetterRoutingKey);
        await cluster.DeclareQueueAsync(
            inputQueue,
            new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = deadLetterRoutingKey
            });
        await cluster.PublishToQueueAsync(inputQueue, "bad");

        var sourceOptions = new RabbitMqSourceOptions
        {
            Connection = CreateConnectionOptions(cluster),
            Queue = RabbitMqQueueOptions.Named(inputQueue)
        };
        sourceOptions.Settlement.PipelineDeadLetterSettlement =
            RabbitMqPipelineDeadLetterSettlement.DeadLetterOrDiscardSourceMessage;

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbitmq-source-dlx-integration")
            .WithRabbitMqSource(
                sourceOptions,
                delivery => new TestRabbitMqRecord
                {
                    Id = delivery.DeliveryTag.ToString(),
                    Value = Encoding.UTF8.GetString(delivery.Body.Span)
                })
            .AddSegment(new FaultingSegment(), new object())
            .WithInMemoryDestination("memory")
            .WithDeadLetterDestination(new InMemoryDeadLetterDestination<TestRabbitMqRecord>())
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync();
        var deadLettered = await cluster.WaitForMessageAsync(deadLetterQueue);
        await cluster.WaitForQueueToDrainAsync(inputQueue);
        await pipeline.CompleteAsync();

        Assert.Equal("bad", Encoding.UTF8.GetString(deadLettered.Body.Span));
    }

    private static RabbitMqConnectionOptions CreateConnectionOptions(RabbitMqTestCluster cluster)
    {
        return new RabbitMqConnectionOptions
        {
            Uri = new Uri(cluster.ConnectionString)
        };
    }

    private static async Task<RabbitMqTestCluster?> TryStartClusterAsync()
    {
        try
        {
            var cluster = new RabbitMqTestCluster();
            await cluster.InitializeAsync();
            return cluster;
        }
        catch (DockerUnavailableException)
        {
            return null;
        }
    }

    private static string FormatHeader(object? value)
    {
        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => value?.ToString() ?? string.Empty
        };
    }

    private sealed class FaultingSegment : PipelineSegment<TestRabbitMqRecord>
    {
        public override Task<TestRabbitMqRecord> ExecuteAsync(TestRabbitMqRecord arg)
        {
            throw new InvalidOperationException("boom");
        }
    }
}

using System.Text;
using Pipelinez.Core;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;
using Pipelinez.RabbitMQ.Tests.Infrastructure;
using Pipelinez.RabbitMQ.Tests.Models;
using RabbitMQ.Client;
using Xunit;

namespace Pipelinez.RabbitMQ.Tests.Unit;

public class RabbitMqDestinationTests
{
    [Fact]
    public async Task Destination_Publishes_Mapped_Message_After_Copying_Headers()
    {
        var factory = new FakeRabbitMqClientFactory();
        var destination = new RabbitMqPipelineDestination<TestRabbitMqRecord>(
            CreateDestinationOptions(),
            record => RabbitMqPublishMessage.Create(
                Encoding.UTF8.GetBytes(record.Value),
                properties: new BasicProperties { MessageId = record.Id }),
            factory);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbit-destination")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        var record = new TestRabbitMqRecord { Id = "1", Value = "hello" };
        record.Headers.Add(new() { Key = "tenant", Value = "north" });

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(record);
        await pipeline.CompleteAsync();

        var message = Assert.Single(factory.Channel.PublishedMessages);
        Assert.Equal("orders", message.Exchange);
        Assert.Equal("processed", message.RoutingKey);
        Assert.True(message.Mandatory);
        Assert.Equal("hello", Encoding.UTF8.GetString(message.Body));
        Assert.Equal("1", message.Properties.MessageId);
        Assert.NotNull(message.Properties.Headers);
        Assert.Equal("north", message.Properties.Headers["tenant"]);
    }

    [Fact]
    public async Task Destination_Preserves_Mapper_Provided_Headers()
    {
        var factory = new FakeRabbitMqClientFactory();
        var destination = new RabbitMqPipelineDestination<TestRabbitMqRecord>(
            CreateDestinationOptions(),
            record =>
            {
                var properties = new BasicProperties
                {
                    MessageId = record.Id,
                    Headers = new Dictionary<string, object?> { ["tenant"] = "mapper" }
                };

                return RabbitMqPublishMessage.Create(
                    Encoding.UTF8.GetBytes(record.Value),
                    properties: properties);
            },
            factory);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbit-destination-headers")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        var record = new TestRabbitMqRecord { Id = "1", Value = "hello" };
        record.Headers.Add(new() { Key = "tenant", Value = "header" });

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(record);
        await pipeline.CompleteAsync();

        var message = Assert.Single(factory.Channel.PublishedMessages);
        Assert.NotNull(message.Properties.Headers);
        Assert.Equal("mapper", message.Properties.Headers["tenant"]);
    }

    [Fact]
    public async Task Destination_Passes_Publisher_Confirm_Timeout_To_Channel()
    {
        var factory = new FakeRabbitMqClientFactory();
        var options = CreateDestinationOptions();
        options.PublisherConfirmTimeout = TimeSpan.FromSeconds(12);
        var destination = new RabbitMqPipelineDestination<TestRabbitMqRecord>(
            options,
            record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Value)),
            factory);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbit-destination-confirm-timeout")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestRabbitMqRecord { Id = "1", Value = "hello" });
        await pipeline.CompleteAsync();

        var message = Assert.Single(factory.Channel.PublishedMessages);
        Assert.Equal(TimeSpan.FromSeconds(12), message.Timeout);
    }

    [Fact]
    public async Task Destination_Propagates_Publish_Failure_To_Error_Handler()
    {
        var factory = new FakeRabbitMqClientFactory();
        factory.Channel.PublishException = new InvalidOperationException("publish failed");
        var faulted = false;
        var destination = new RabbitMqPipelineDestination<TestRabbitMqRecord>(
            CreateDestinationOptions(),
            record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes(record.Value)),
            factory);

        var pipeline = Pipeline<TestRabbitMqRecord>.New("rabbit-destination-publish-failure")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .WithErrorHandler(_ =>
            {
                faulted = true;
                return Pipelinez.Core.ErrorHandling.PipelineErrorAction.SkipRecord;
            })
            .Build();

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestRabbitMqRecord { Id = "1", Value = "hello" });
        await pipeline.CompleteAsync();

        Assert.True(faulted);
        Assert.Empty(factory.Channel.PublishedMessages);
    }

    private static RabbitMqDestinationOptions CreateDestinationOptions()
    {
        return new RabbitMqDestinationOptions
        {
            Connection = new RabbitMqConnectionOptions
            {
                Uri = new Uri("amqp://guest:guest@localhost:5672/")
            },
            Exchange = "orders",
            RoutingKey = "processed"
        };
    }
}

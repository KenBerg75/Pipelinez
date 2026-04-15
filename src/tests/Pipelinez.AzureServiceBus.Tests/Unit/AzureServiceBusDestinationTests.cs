using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.Destination;
using Pipelinez.AzureServiceBus.Tests.Infrastructure;
using Pipelinez.AzureServiceBus.Tests.Models;
using Pipelinez.Core;
using Xunit;

namespace Pipelinez.AzureServiceBus.Tests.Unit;

public class AzureServiceBusDestinationTests
{
    [Fact]
    public async Task Destination_Sends_Mapped_Message_After_Copying_Headers()
    {
        var factory = new FakeAzureServiceBusClientFactory();
        var destination = new AzureServiceBusPipelineDestination<TestAzureServiceBusRecord>(
            CreateDestinationOptions(),
            record => new ServiceBusMessage(BinaryData.FromString(record.Value))
            {
                MessageId = record.Id
            },
            factory);

        var pipeline = Pipeline<TestAzureServiceBusRecord>.New("asb-destination")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        var record = new TestAzureServiceBusRecord { Id = "1", Value = "hello" };
        record.Headers.Add(new() { Key = "tenant", Value = "north" });

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(record);
        await pipeline.CompleteAsync();

        var message = Assert.Single(factory.Sender.Messages);
        Assert.Equal("1", message.MessageId);
        Assert.Equal("hello", message.Body.ToString());
        Assert.Equal("north", message.ApplicationProperties["tenant"]);
    }

    [Fact]
    public async Task Destination_Preserves_Mapper_Provided_Application_Properties()
    {
        var factory = new FakeAzureServiceBusClientFactory();
        var destination = new AzureServiceBusPipelineDestination<TestAzureServiceBusRecord>(
            CreateDestinationOptions(),
            _ =>
            {
                var message = new ServiceBusMessage(BinaryData.FromString("body"));
                message.ApplicationProperties["tenant"] = "mapper";
                return message;
            },
            factory);

        var pipeline = Pipeline<TestAzureServiceBusRecord>.New("asb-destination-properties")
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        var record = new TestAzureServiceBusRecord { Id = "1", Value = "hello" };
        record.Headers.Add(new() { Key = "tenant", Value = "header" });

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(record);
        await pipeline.CompleteAsync();

        var message = Assert.Single(factory.Sender.Messages);
        Assert.Equal("mapper", message.ApplicationProperties["tenant"]);
    }

    private static AzureServiceBusDestinationOptions CreateDestinationOptions()
    {
        return new AzureServiceBusDestinationOptions
        {
            Connection = new AzureServiceBusConnectionOptions
            {
                ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
            },
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-out")
        };
    }
}

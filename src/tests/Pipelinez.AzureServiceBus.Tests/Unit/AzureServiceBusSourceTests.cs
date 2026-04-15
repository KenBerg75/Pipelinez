using Azure.Messaging.ServiceBus;
using Pipelinez.AzureServiceBus.Configuration;
using Pipelinez.AzureServiceBus.Source;
using Pipelinez.AzureServiceBus.Tests.Infrastructure;
using Pipelinez.AzureServiceBus.Tests.Models;
using Pipelinez.Core;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Destination;
using Xunit;

namespace Pipelinez.AzureServiceBus.Tests.Unit;

public class AzureServiceBusSourceTests
{
    [Fact]
    public void Source_Reports_Distributed_Logical_Lease()
    {
        var factory = new FakeAzureServiceBusClientFactory();
        var source = new AzureServiceBusPipelineSource<TestAzureServiceBusRecord>(
            CreateSourceOptions(),
            _ => new TestAzureServiceBusRecord(),
            factory);

        var pipeline = Pipeline<TestAzureServiceBusRecord>.New("asb-source")
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "node-a",
                WorkerId = "worker-a"
            })
            .WithSource(source)
            .WithDestination(new InMemoryPipelineDestination<TestAzureServiceBusRecord>())
            .Build();

        var status = pipeline.GetStatus().DistributedStatus;

        Assert.NotNull(status);
        var lease = Assert.Single(status.OwnedPartitions);
        Assert.Equal("AzureServiceBus", lease.TransportName);
        Assert.Equal("asb:Queue:orders-in", lease.LeaseId);
        Assert.Equal("orders-in", lease.PartitionKey);
        Assert.True(source.SupportsDistributedExecution);
        Assert.Equal("AzureServiceBus", source.TransportName);
        Assert.True(factory.Processor.HasMessageHandler);
        Assert.True(factory.Processor.HasErrorHandler);
    }

    private static AzureServiceBusSourceOptions CreateSourceOptions()
    {
        return new AzureServiceBusSourceOptions
        {
            Connection = new AzureServiceBusConnectionOptions
            {
                ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
            },
            Entity = AzureServiceBusEntityOptions.ForQueue("orders-in")
        };
    }
}

using Pipelinez.RabbitMQ.Configuration;
using Xunit;

namespace Pipelinez.RabbitMQ.Tests.Unit;

public class RabbitMqOptionsTests
{
    [Fact]
    public void Connection_Options_Accept_Uri()
    {
        var options = new RabbitMqConnectionOptions
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672/")
        };

        Assert.Same(options, options.Validate());
    }

    [Fact]
    public void Connection_Options_Accept_Host_Name()
    {
        var options = new RabbitMqConnectionOptions
        {
            HostName = "localhost"
        };

        Assert.Same(options, options.Validate());
    }

    [Fact]
    public void Connection_Options_Reject_Mixed_Connection_Styles()
    {
        var options = new RabbitMqConnectionOptions
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672/"),
            HostName = "localhost"
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Source_Requires_Queue_Name()
    {
        var options = CreateSourceOptions();
        options.Queue = new RabbitMqQueueOptions();

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Source_Rejects_Invalid_Prefetch()
    {
        var options = CreateSourceOptions();
        options.PrefetchCount = 0;

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Destination_Allows_Default_Exchange_With_Routing_Key()
    {
        var options = new RabbitMqDestinationOptions
        {
            Connection = CreateConnection(),
            Exchange = string.Empty,
            RoutingKey = "orders-out"
        };

        Assert.Same(options, options.Validate());
    }

    [Fact]
    public void Destination_Rejects_Default_Exchange_Without_Routing_Key()
    {
        var options = new RabbitMqDestinationOptions
        {
            Connection = CreateConnection(),
            Exchange = string.Empty,
            RoutingKey = string.Empty
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Topology_Requires_Queue_And_Exchange_For_Binding()
    {
        var topology = new RabbitMqTopologyOptions
        {
            BindQueue = true,
            Queue = RabbitMqQueueOptions.Named("orders")
        };

        Assert.Throws<InvalidOperationException>(() => topology.Validate());
    }

    private static RabbitMqSourceOptions CreateSourceOptions()
    {
        return new RabbitMqSourceOptions
        {
            Connection = CreateConnection(),
            Queue = RabbitMqQueueOptions.Named("orders-in")
        };
    }

    private static RabbitMqConnectionOptions CreateConnection()
    {
        return new RabbitMqConnectionOptions
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672/")
        };
    }
}

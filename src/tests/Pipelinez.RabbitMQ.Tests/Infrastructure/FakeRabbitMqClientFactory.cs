using Pipelinez.RabbitMQ.Client;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Source;
using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Tests.Infrastructure;

internal sealed class FakeRabbitMqClientFactory : IRabbitMqClientFactory
{
    public FakeRabbitMqChannel Channel { get; } = new();

    public Task<IRabbitMqChannel> CreateSourceChannelAsync(
        RabbitMqSourceOptions options,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IRabbitMqChannel>(Channel);
    }

    public Task<IRabbitMqChannel> CreateDestinationChannelAsync(
        RabbitMqDestinationOptions options,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IRabbitMqChannel>(Channel);
    }
}

internal sealed class FakeRabbitMqChannel : IRabbitMqChannel
{
    private Func<RabbitMqDeliveryContext, Task>? _handleDeliveryAsync;
    private readonly List<PublishedRabbitMqMessage> _publishedMessages = new();
    private readonly List<(ulong DeliveryTag, string Action, bool Requeue)> _settlements = new();

    public bool IsOpen { get; set; } = true;

    public bool SourceConfigured { get; private set; }

    public bool DestinationConfigured { get; private set; }

    public bool Canceled { get; private set; }

    public Exception? PublishException { get; set; }

    public IReadOnlyList<PublishedRabbitMqMessage> PublishedMessages => _publishedMessages.ToArray();

    public IReadOnlyList<(ulong DeliveryTag, string Action, bool Requeue)> Settlements => _settlements.ToArray();

    public Task ConfigureSourceAsync(
        RabbitMqSourceOptions options,
        CancellationToken cancellationToken)
    {
        SourceConfigured = true;
        return Task.CompletedTask;
    }

    public Task ConfigureDestinationAsync(
        RabbitMqDestinationOptions options,
        CancellationToken cancellationToken)
    {
        DestinationConfigured = true;
        return Task.CompletedTask;
    }

    public Task<string> BasicConsumeAsync(
        RabbitMqSourceOptions options,
        Func<RabbitMqDeliveryContext, Task> handleDeliveryAsync,
        CancellationToken cancellationToken)
    {
        _handleDeliveryAsync = handleDeliveryAsync;
        return Task.FromResult("fake-consumer");
    }

    public Task BasicCancelAsync(
        string consumerTag,
        CancellationToken cancellationToken)
    {
        Canceled = true;
        return Task.CompletedTask;
    }

    public Task BasicAckAsync(
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        _settlements.Add((deliveryTag, "ack", false));
        return Task.CompletedTask;
    }

    public Task BasicNackAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        _settlements.Add((deliveryTag, "nack", requeue));
        return Task.CompletedTask;
    }

    public Task BasicRejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        _settlements.Add((deliveryTag, "reject", requeue));
        return Task.CompletedTask;
    }

    public Task BasicPublishAsync(
        string exchange,
        string routingKey,
        bool mandatory,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (PublishException is not null)
        {
            throw PublishException;
        }

        _publishedMessages.Add(new PublishedRabbitMqMessage(exchange, routingKey, mandatory, properties, body.ToArray(), timeout));
        return Task.CompletedTask;
    }

    public async Task DeliverAsync(RabbitMqDeliveryContext delivery)
    {
        if (_handleDeliveryAsync is null)
        {
            throw new InvalidOperationException("No RabbitMQ delivery handler has been registered.");
        }

        await _handleDeliveryAsync(delivery).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        return ValueTask.CompletedTask;
    }
}

internal sealed record PublishedRabbitMqMessage(
    string Exchange,
    string RoutingKey,
    bool Mandatory,
    BasicProperties Properties,
    byte[] Body,
    TimeSpan? Timeout);

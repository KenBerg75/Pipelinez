using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Pipelinez.RabbitMQ.Tests.Infrastructure;

public sealed class RabbitMqTestCluster : IAsyncLifetime, IAsyncDisposable
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:3.13-management").Build();

    public string ConnectionString => _container.GetConnectionString();

    public TimeSpan ObservationTimeout { get; } = TimeSpan.FromSeconds(20);

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        return DisposeAsyncCore().AsTask();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
    }

    public async Task DeclareQueueAsync(
        string queueName,
        IDictionary<string, object?>? arguments = null)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        await channel
            .QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments)
            .ConfigureAwait(false);
    }

    public async Task DeclareExchangeAsync(string exchangeName)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        await channel
            .ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false)
            .ConfigureAwait(false);
    }

    public async Task BindQueueAsync(
        string queueName,
        string exchangeName,
        string routingKey)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        await channel
            .QueueBindAsync(
                queueName,
                exchangeName,
                routingKey)
            .ConfigureAwait(false);
    }

    public async Task PublishToQueueAsync(string queueName, string body, params (string Key, string Value)[] headers)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = headers.ToDictionary(header => header.Key, header => (object?)header.Value)
        };

        await channel.BasicPublishAsync(
                string.Empty,
                queueName,
                mandatory: true,
                properties,
                System.Text.Encoding.UTF8.GetBytes(body))
            .ConfigureAwait(false);
    }

    public async Task<BasicGetResult?> GetMessageAsync(string queueName)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        return await channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
    }

    public async Task<BasicGetResult> WaitForMessageAsync(string queueName)
    {
        var deadline = DateTimeOffset.UtcNow + ObservationTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var message = await GetMessageAsync(queueName).ConfigureAwait(false);
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for RabbitMQ message in queue '{queueName}'.");
    }

    public async Task WaitForQueueToDrainAsync(string queueName)
    {
        var deadline = DateTimeOffset.UtcNow + ObservationTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
            await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var declaration = await channel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
            if (declaration.MessageCount == 0)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for RabbitMQ queue '{queueName}' to drain.");
    }

    private Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(ConnectionString)
        };
        return factory.CreateConnectionAsync();
    }

    private ValueTask DisposeAsyncCore()
    {
        return _container.DisposeAsync();
    }
}

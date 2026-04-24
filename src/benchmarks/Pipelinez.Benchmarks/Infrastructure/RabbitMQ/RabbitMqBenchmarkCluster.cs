using System.Text;
using Pipelinez.RabbitMQ.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;

namespace Pipelinez.Benchmarks;

internal sealed class RabbitMqBenchmarkCluster : IAsyncDisposable
{
    private const string DefaultImage = "rabbitmq:3.13-management";
    private readonly RabbitMqContainer _container;

    public RabbitMqBenchmarkCluster()
    {
        var image = Environment.GetEnvironmentVariable("PIPELINEZ_BENCH_RABBITMQ_IMAGE")
                    ?? Environment.GetEnvironmentVariable("PIPELINEZ_RABBITMQ_TEST_IMAGE")
                    ?? DefaultImage;

        _container = new RabbitMqBuilder(image).Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public RabbitMqConnectionOptions CreateConnectionOptions()
    {
        return new RabbitMqConnectionOptions
        {
            Uri = new Uri(ConnectionString)
        };
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    public async Task DeclareQueueAsync(string queueName, IDictionary<string, object?>? arguments = null)
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

    public async Task BindQueueAsync(string queueName, string exchangeName, string routingKey)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey).ConfigureAwait(false);
    }

    public async Task PublishMessagesToQueueAsync(string queueName, IEnumerable<string> payloads)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        foreach (var payload in payloads)
        {
            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                    string.Empty,
                    queueName,
                    mandatory: true,
                    properties,
                    Encoding.UTF8.GetBytes(payload))
                .ConfigureAwait(false);
        }
    }

    public async Task<uint> GetMessageCountAsync(string queueName)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        var declaration = await channel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
        return declaration.MessageCount;
    }

    public Task WaitForMessageCountAsync(string queueName, uint expectedCount, TimeSpan timeout)
    {
        return BenchmarkPolling.WaitUntilAsync(
            async () => await GetMessageCountAsync(queueName).ConfigureAwait(false) >= expectedCount,
            timeout,
            $"Timed out waiting for {expectedCount} RabbitMQ messages in queue '{queueName}'.");
    }

    public Task WaitForQueueToDrainAsync(string queueName, TimeSpan timeout)
    {
        return BenchmarkPolling.WaitUntilAsync(
            async () => await GetMessageCountAsync(queueName).ConfigureAwait(false) == 0,
            timeout,
            $"Timed out waiting for RabbitMQ queue '{queueName}' to drain.");
    }

    public async Task<IReadOnlyList<string>> DrainQueueAsync(string queueName)
    {
        var messages = new List<string>();
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        while (true)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
            if (result is null)
            {
                return messages;
            }

            messages.Add(Encoding.UTF8.GetString(result.Body.Span));
        }
    }

    public async Task DeleteQueueIfExistsAsync(string queueName)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        try
        {
            await channel.QueueDeleteAsync(queueName, ifUnused: false, ifEmpty: false).ConfigureAwait(false);
        }
        catch (OperationInterruptedException)
        {
        }
    }

    public async Task DeleteExchangeIfExistsAsync(string exchangeName)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        try
        {
            await channel.ExchangeDeleteAsync(exchangeName, ifUnused: false).ConfigureAwait(false);
        }
        catch (OperationInterruptedException)
        {
        }
    }

    private Task<IConnection> CreateConnectionAsync()
    {
        return new ConnectionFactory
        {
            Uri = new Uri(ConnectionString)
        }.CreateConnectionAsync();
    }
}

using System.Text;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.RabbitMQ;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Destination;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

const string InputQueue = "pipelinez-example-orders-in";
const string OutputQueue = "pipelinez-example-orders-out";
const string DeadLetterQueue = "pipelinez-example-orders-dead";

await using var broker = await RabbitMqExampleBroker.StartAsync();
var connection = new RabbitMqConnectionOptions
{
    Uri = new Uri(broker.ConnectionString)
};

await broker.DeclareQueueAsync(InputQueue);
await broker.DeclareQueueAsync(OutputQueue);
await broker.DeclareQueueAsync(DeadLetterQueue);
await broker.PublishAsync(InputQueue, "A-100|ok");
await broker.PublishAsync(InputQueue, "A-200|fail");
await broker.PublishAsync(InputQueue, "A-300|ok");

var pipeline = Pipeline<OrderRecord>.New("rabbitmq-orders")
    .WithRabbitMqSource(
        new RabbitMqSourceOptions
        {
            Connection = connection,
            Queue = RabbitMqQueueOptions.Named(InputQueue),
            PrefetchCount = 4
        },
        delivery =>
        {
            var parts = Encoding.UTF8.GetString(delivery.Body.Span).Split('|');
            return new OrderRecord
            {
                Id = parts[0],
                Payload = parts[1]
            };
        })
    .AddSegment(new ValidateOrderSegment(), new object())
    .WithRabbitMqDestination(
        new RabbitMqDestinationOptions
        {
            Connection = connection,
            RoutingKey = OutputQueue
        },
        record => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes($"{record.Id}|processed")))
    .WithRabbitMqDeadLetterDestination(
        new RabbitMqDeadLetterOptions
        {
            Connection = connection,
            RoutingKey = DeadLetterQueue
        },
        deadLetter => RabbitMqPublishMessage.Create(Encoding.UTF8.GetBytes($"{deadLetter.Record.Id}|dead-letter")))
    .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
    .Build();

await pipeline.StartPipelineAsync();
await broker.WaitForMessageCountAsync(OutputQueue, 2);
await broker.WaitForMessageCountAsync(DeadLetterQueue, 1);
await pipeline.CompleteAsync();

var processed = await broker.DrainAsync(OutputQueue);
var failed = await broker.DrainAsync(DeadLetterQueue);

Console.WriteLine("Processed:");
foreach (var message in processed)
{
    Console.WriteLine($"  {message}");
}

Console.WriteLine("Dead-lettered:");
foreach (var message in failed)
{
    Console.WriteLine($"  {message}");
}

public sealed class OrderRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Payload { get; init; }
}

public sealed class ValidateOrderSegment : PipelineSegment<OrderRecord>
{
    public override Task<OrderRecord> ExecuteAsync(OrderRecord arg)
    {
        if (arg.Payload == "fail")
        {
            throw new InvalidOperationException($"Order {arg.Id} failed validation.");
        }

        return Task.FromResult(arg);
    }
}

internal sealed class RabbitMqExampleBroker : IAsyncDisposable
{
    private readonly RabbitMqContainer? _container;

    private RabbitMqExampleBroker(string connectionString, RabbitMqContainer? container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    public string ConnectionString { get; }

    public static async Task<RabbitMqExampleBroker> StartAsync()
    {
        var existingConnectionString = Environment.GetEnvironmentVariable("PIPELINEZ_EXAMPLE_RABBITMQ_URI");
        if (!string.IsNullOrWhiteSpace(existingConnectionString))
        {
            return new RabbitMqExampleBroker(existingConnectionString, container: null);
        }

        var container = new RabbitMqBuilder("rabbitmq:3.13-management").Build();
        await container.StartAsync().ConfigureAwait(false);
        return new RabbitMqExampleBroker(container.GetConnectionString(), container);
    }

    public async Task DeclareQueueAsync(string queueName)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false).ConfigureAwait(false);
    }

    public async Task PublishAsync(string queueName, string body)
    {
        await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        var properties = new BasicProperties
        {
            Persistent = true
        };
        await channel.BasicPublishAsync(
                string.Empty,
                queueName,
                mandatory: true,
                properties,
                Encoding.UTF8.GetBytes(body))
            .ConfigureAwait(false);
    }

    public async Task WaitForMessageCountAsync(string queueName, uint expectedCount)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = await CreateConnectionAsync().ConfigureAwait(false);
            await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var declaration = await channel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);
            if (declaration.MessageCount >= expectedCount)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for {expectedCount} messages in queue {queueName}.");
    }

    public async Task<IReadOnlyList<string>> DrainAsync(string queueName)
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

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
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

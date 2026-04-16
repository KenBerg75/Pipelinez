using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Source;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Pipelinez.RabbitMQ.Client;

internal sealed class RabbitMqChannel : IRabbitMqChannel
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    public RabbitMqChannel(IConnection connection, IChannel channel)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    public bool IsOpen => _channel.IsOpen;

    public async Task ConfigureSourceAsync(
        RabbitMqSourceOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        await ApplyTopologyAsync(validated.Topology, cancellationToken).ConfigureAwait(false);
        await _channel
            .BasicQosAsync(0, validated.PrefetchCount, validated.GlobalQos, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task ConfigureDestinationAsync(
        RabbitMqDestinationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ApplyTopologyAsync(options.Validate().Topology, cancellationToken);
    }

    public Task<string> BasicConsumeAsync(
        RabbitMqSourceOptions options,
        Func<RabbitMqDeliveryContext, Task> handleDeliveryAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handleDeliveryAsync);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            var bodyCopy = args.Body.ToArray();
            await handleDeliveryAsync(new RabbitMqDeliveryContext
                {
                    ConsumerTag = args.ConsumerTag,
                    DeliveryTag = args.DeliveryTag,
                    Redelivered = args.Redelivered,
                    Exchange = args.Exchange,
                    RoutingKey = args.RoutingKey,
                    Body = bodyCopy,
                    Properties = args.BasicProperties
                })
                .ConfigureAwait(false);
        };

        return _channel.BasicConsumeAsync(
            options.Queue.Name,
            autoAck: false,
            consumerTag: options.ConsumerTag ?? string.Empty,
            noLocal: options.NoLocal,
            exclusive: options.ExclusiveConsumer,
            arguments: ConvertArguments(options.ConsumerArguments),
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    public Task BasicCancelAsync(
        string consumerTag,
        CancellationToken cancellationToken)
    {
        return _channel.BasicCancelAsync(consumerTag, noWait: false, cancellationToken);
    }

    public async Task BasicAckAsync(
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        await _channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task BasicNackAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue, cancellationToken).ConfigureAwait(false);
    }

    public async Task BasicRejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await _channel.BasicRejectAsync(deliveryTag, requeue, cancellationToken).ConfigureAwait(false);
    }

    public async Task BasicPublishAsync(
        string exchange,
        string routingKey,
        bool mandatory,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var publishTask = _channel
                .BasicPublishAsync(
                    exchange,
                    routingKey,
                    mandatory,
                    properties,
                    body,
                    cancellationToken)
                .AsTask();

            if (timeout.HasValue)
            {
                await publishTask.WaitAsync(timeout.Value, cancellationToken).ConfigureAwait(false);
                return;
            }

            await publishTask.ConfigureAwait(false);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _publishLock.Dispose();
        await _channel.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ApplyTopologyAsync(
        RabbitMqTopologyOptions topology,
        CancellationToken cancellationToken)
    {
        topology.Validate();

        if (topology.PassiveDeclareOnly)
        {
            if (topology.Exchange is not null)
            {
                await _channel.ExchangeDeclarePassiveAsync(topology.Exchange.Name, cancellationToken).ConfigureAwait(false);
            }

            if (topology.Queue is not null)
            {
                await _channel.QueueDeclarePassiveAsync(topology.Queue.Name, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (topology.DeclareExchange && topology.Exchange is not null && !string.IsNullOrWhiteSpace(topology.Exchange.Name))
        {
            await _channel.ExchangeDeclareAsync(
                    topology.Exchange.Name,
                    topology.Exchange.GetExchangeTypeName(),
                    topology.Exchange.Durable,
                    topology.Exchange.AutoDelete,
                    ConvertArguments(topology.Exchange.Arguments),
                    passive: false,
                    noWait: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (topology.DeclareQueue && topology.Queue is not null)
        {
            await _channel.QueueDeclareAsync(
                    topology.Queue.Name,
                    topology.Queue.Durable,
                    topology.Queue.Exclusive,
                    topology.Queue.AutoDelete,
                    ConvertArguments(topology.Queue.Arguments),
                    passive: false,
                    noWait: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (topology.BindQueue && topology.Queue is not null && topology.Exchange is not null)
        {
            await _channel.QueueBindAsync(
                    topology.Queue.Name,
                    topology.Exchange.Name,
                    topology.BindingRoutingKey ?? string.Empty,
                    ConvertArguments(topology.BindingArguments),
                    noWait: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IDictionary<string, object?>? ConvertArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        return arguments.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
    }
}

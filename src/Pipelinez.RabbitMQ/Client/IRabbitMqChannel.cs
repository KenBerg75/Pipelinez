using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.Source;
using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Client;

internal interface IRabbitMqChannel : IAsyncDisposable
{
    bool IsOpen { get; }

    Task ConfigureSourceAsync(
        RabbitMqSourceOptions options,
        CancellationToken cancellationToken);

    Task ConfigureDestinationAsync(
        RabbitMqDestinationOptions options,
        CancellationToken cancellationToken);

    Task<string> BasicConsumeAsync(
        RabbitMqSourceOptions options,
        Func<RabbitMqDeliveryContext, Task> handleDeliveryAsync,
        CancellationToken cancellationToken);

    Task BasicCancelAsync(
        string consumerTag,
        CancellationToken cancellationToken);

    Task BasicAckAsync(
        ulong deliveryTag,
        CancellationToken cancellationToken);

    Task BasicNackAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken);

    Task BasicRejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken);

    Task BasicPublishAsync(
        string exchange,
        string routingKey,
        bool mandatory,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        TimeSpan? timeout,
        CancellationToken cancellationToken);
}

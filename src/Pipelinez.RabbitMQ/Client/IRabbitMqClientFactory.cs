using Pipelinez.RabbitMQ.Configuration;

namespace Pipelinez.RabbitMQ.Client;

internal interface IRabbitMqClientFactory
{
    Task<IRabbitMqChannel> CreateSourceChannelAsync(
        RabbitMqSourceOptions options,
        CancellationToken cancellationToken);

    Task<IRabbitMqChannel> CreateDestinationChannelAsync(
        RabbitMqDestinationOptions options,
        CancellationToken cancellationToken);
}

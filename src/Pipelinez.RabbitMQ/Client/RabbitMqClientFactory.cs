using Pipelinez.RabbitMQ.Configuration;
using RabbitMQ.Client;

namespace Pipelinez.RabbitMQ.Client;

internal sealed class RabbitMqClientFactory : IRabbitMqClientFactory
{
    public static RabbitMqClientFactory Instance { get; } = new();

    public async Task<IRabbitMqChannel> CreateSourceChannelAsync(
        RabbitMqSourceOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        var connection = await CreateConnectionAsync(
                validated.Connection,
                "Pipelinez.RabbitMQ.Source",
                cancellationToken)
            .ConfigureAwait(false);
        var channel = await connection
            .CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: validated.ConsumerDispatchConcurrency), cancellationToken)
            .ConfigureAwait(false);
        return new RabbitMqChannel(connection, channel);
    }

    public async Task<IRabbitMqChannel> CreateDestinationChannelAsync(
        RabbitMqDestinationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validate();
        var connection = await CreateConnectionAsync(
                validated.Connection,
                "Pipelinez.RabbitMQ.Destination",
                cancellationToken)
            .ConfigureAwait(false);
        var channel = await connection
            .CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: validated.UsePublisherConfirms,
                publisherConfirmationTrackingEnabled: validated.UsePublisherConfirms,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: null), cancellationToken)
            .ConfigureAwait(false);
        return new RabbitMqChannel(connection, channel);
    }

    private static Task<IConnection> CreateConnectionAsync(
        RabbitMqConnectionOptions options,
        string defaultClientProvidedName,
        CancellationToken cancellationToken)
    {
        var factory = options.CreateConnectionFactory(defaultClientProvidedName);
        return factory.CreateConnectionAsync(cancellationToken);
    }
}

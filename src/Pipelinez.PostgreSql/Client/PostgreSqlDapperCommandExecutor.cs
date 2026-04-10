using Ardalis.GuardClauses;
using Dapper;
using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.PostgreSql.Client;

internal sealed class PostgreSqlDapperCommandExecutor : IPostgreSqlCommandExecutor, IAsyncDisposable
{
    private readonly IPostgreSqlConnectionFactory _connectionFactory;
    private readonly int? _defaultCommandTimeoutSeconds;

    internal PostgreSqlDapperCommandExecutor(
        IPostgreSqlConnectionFactory connectionFactory,
        int? defaultCommandTimeoutSeconds)
    {
        _connectionFactory = Guard.Against.Null(connectionFactory, nameof(connectionFactory));
        _defaultCommandTimeoutSeconds = defaultCommandTimeoutSeconds;
    }

    public async Task ExecuteAsync(PostgreSqlCommandDefinition command, CancellationToken cancellationToken)
    {
        var validatedCommand = Guard.Against.Null(command, nameof(command)).Validate();

        await using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
                validatedCommand.CommandText,
                validatedCommand.Parameters,
                commandTimeout: validatedCommand.CommandTimeoutSeconds ?? _defaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        return _connectionFactory.DisposeAsync();
    }
}

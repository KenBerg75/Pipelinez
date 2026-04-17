using Ardalis.GuardClauses;
using Dapper;
using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.SqlServer.Client;

internal sealed class SqlServerDapperCommandExecutor : ISqlServerCommandExecutor, IAsyncDisposable
{
    private readonly ISqlServerConnectionFactory _connectionFactory;
    private readonly int? _defaultCommandTimeoutSeconds;

    internal SqlServerDapperCommandExecutor(
        ISqlServerConnectionFactory connectionFactory,
        int? defaultCommandTimeoutSeconds)
    {
        _connectionFactory = Guard.Against.Null(connectionFactory, nameof(connectionFactory));
        _defaultCommandTimeoutSeconds = defaultCommandTimeoutSeconds;
    }

    public async Task ExecuteAsync(SqlServerCommandDefinition command, CancellationToken cancellationToken)
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

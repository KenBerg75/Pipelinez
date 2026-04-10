using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.PostgreSql.Client;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.PostgreSql.DeadLettering;

internal sealed class PostgreSqlDeadLetterDestination<TRecord> : IPipelineDeadLetterDestination<TRecord>
    where TRecord : PipelineRecord
{
    private readonly Func<PipelineDeadLetterRecord<TRecord>, PostgreSqlCommandDefinition> _commandFactory;
    private readonly ILogger<PostgreSqlDeadLetterDestination<TRecord>> _logger;
    private readonly IPostgreSqlCommandExecutor _executor;

    internal PostgreSqlDeadLetterDestination(
        PostgreSqlDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<TRecord>, PostgreSqlCommandDefinition> commandFactory,
        IPostgreSqlCommandExecutor? executorOverride = null)
    {
        var validatedOptions = Guard.Against.Null(options, nameof(options)).Validate();
        _commandFactory = Guard.Against.Null(commandFactory, nameof(commandFactory));
        _logger = LoggingManager.Instance.CreateLogger<PostgreSqlDeadLetterDestination<TRecord>>();
        _executor = executorOverride ?? new PostgreSqlDapperCommandExecutor(
            new PostgreSqlConnectionFactory(validatedOptions),
            validatedOptions.CommandTimeoutSeconds);
    }

    public async Task WriteAsync(PipelineDeadLetterRecord<TRecord> deadLetterRecord, CancellationToken cancellationToken)
    {
        Guard.Against.Null(deadLetterRecord, nameof(deadLetterRecord));
        deadLetterRecord.Validate();

        var command = _commandFactory(deadLetterRecord).Validate();
        _logger.LogTrace("Writing PostgreSQL dead-letter record");
        await _executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }
}

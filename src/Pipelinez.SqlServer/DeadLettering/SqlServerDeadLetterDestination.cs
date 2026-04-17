using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.SqlServer.Client;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.SqlServer.DeadLettering;

internal sealed class SqlServerDeadLetterDestination<TRecord> : IPipelineDeadLetterDestination<TRecord>
    where TRecord : PipelineRecord
{
    private readonly Func<PipelineDeadLetterRecord<TRecord>, SqlServerCommandDefinition> _commandFactory;
    private readonly ILogger<SqlServerDeadLetterDestination<TRecord>> _logger;
    private readonly ISqlServerCommandExecutor _executor;

    internal SqlServerDeadLetterDestination(
        SqlServerDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<TRecord>, SqlServerCommandDefinition> commandFactory,
        ISqlServerCommandExecutor? executorOverride = null)
    {
        var validatedOptions = Guard.Against.Null(options, nameof(options)).Validate();
        _commandFactory = Guard.Against.Null(commandFactory, nameof(commandFactory));
        _logger = LoggingManager.Instance.CreateLogger<SqlServerDeadLetterDestination<TRecord>>();
        _executor = executorOverride ?? new SqlServerDapperCommandExecutor(
            new SqlServerConnectionFactory(validatedOptions),
            validatedOptions.CommandTimeoutSeconds);
    }

    public async Task WriteAsync(PipelineDeadLetterRecord<TRecord> deadLetterRecord, CancellationToken cancellationToken)
    {
        Guard.Against.Null(deadLetterRecord, nameof(deadLetterRecord));
        deadLetterRecord.Validate();

        var command = _commandFactory(deadLetterRecord).Validate();
        _logger.LogTrace("Writing SQL Server dead-letter record");
        await _executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }
}

using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.PostgreSql.Client;
using Pipelinez.PostgreSql.Configuration;
using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.PostgreSql.Destination;

internal sealed class PostgreSqlPipelineDestination<TRecord> : PipelineDestination<TRecord>
    where TRecord : PipelineRecord
{
    private readonly PostgreSqlDestinationOptions _options;
    private readonly Func<TRecord, PostgreSqlCommandDefinition> _commandFactory;
    private readonly ILogger<PostgreSqlPipelineDestination<TRecord>> _logger;
    private readonly IPostgreSqlCommandExecutor? _executorOverride;
    private IPostgreSqlCommandExecutor? _executor;

    internal PostgreSqlPipelineDestination(
        PostgreSqlDestinationOptions options,
        Func<TRecord, PostgreSqlCommandDefinition> commandFactory,
        IPostgreSqlCommandExecutor? executorOverride = null)
    {
        _options = Guard.Against.Null(options, nameof(options)).Validate();
        _commandFactory = Guard.Against.Null(commandFactory, nameof(commandFactory));
        _executorOverride = executorOverride;
        _logger = LoggingManager.Instance.CreateLogger<PostgreSqlPipelineDestination<TRecord>>();
    }

    protected override void Initialize()
    {
        _logger.LogInformation("Initializing PostgreSQL pipeline destination");
        _executor = _executorOverride ?? new PostgreSqlDapperCommandExecutor(
            new PostgreSqlConnectionFactory(_options),
            _options.CommandTimeoutSeconds);
    }

    protected override async Task ExecuteAsync(TRecord record, CancellationToken cancellationToken)
    {
        var command = _commandFactory(record).Validate();
        await Executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private IPostgreSqlCommandExecutor Executor =>
        _executor ?? throw new InvalidOperationException("PostgreSQL destination has not been initialized.");
}

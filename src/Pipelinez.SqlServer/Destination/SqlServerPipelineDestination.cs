using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.SqlServer.Client;
using Pipelinez.SqlServer.Configuration;
using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.SqlServer.Destination;

internal sealed class SqlServerPipelineDestination<TRecord> : PipelineDestination<TRecord>
    where TRecord : PipelineRecord
{
    private readonly SqlServerDestinationOptions _options;
    private readonly Func<TRecord, SqlServerCommandDefinition> _commandFactory;
    private readonly ILogger<SqlServerPipelineDestination<TRecord>> _logger;
    private readonly ISqlServerCommandExecutor? _executorOverride;
    private ISqlServerCommandExecutor? _executor;

    internal SqlServerPipelineDestination(
        SqlServerDestinationOptions options,
        Func<TRecord, SqlServerCommandDefinition> commandFactory,
        ISqlServerCommandExecutor? executorOverride = null)
    {
        _options = Guard.Against.Null(options, nameof(options)).Validate();
        _commandFactory = Guard.Against.Null(commandFactory, nameof(commandFactory));
        _executorOverride = executorOverride;
        _logger = LoggingManager.Instance.CreateLogger<SqlServerPipelineDestination<TRecord>>();
    }

    protected override void Initialize()
    {
        _logger.LogInformation("Initializing SQL Server pipeline destination");
        _executor = _executorOverride ?? new SqlServerDapperCommandExecutor(
            new SqlServerConnectionFactory(_options),
            _options.CommandTimeoutSeconds);
    }

    protected override async Task ExecuteAsync(TRecord record, CancellationToken cancellationToken)
    {
        var command = _commandFactory(record).Validate();
        await Executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private ISqlServerCommandExecutor Executor =>
        _executor ?? throw new InvalidOperationException("SQL Server destination has not been initialized.");
}

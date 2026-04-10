using Ardalis.GuardClauses;
using Npgsql;
using Pipelinez.PostgreSql.Configuration;

namespace Pipelinez.PostgreSql.Client;

internal sealed class PostgreSqlConnectionFactory : IPostgreSqlConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;

    internal PostgreSqlConnectionFactory(PostgreSqlOptions options)
    {
        var configuration = Guard.Against.Null(options, nameof(options));
        configuration.ValidateCore(configuration.GetType().Name);

        if (configuration.DataSource is not null)
        {
            _dataSource = configuration.DataSource;
            _ownsDataSource = false;
            return;
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuration.ConnectionString);
        configuration.ConfigureConnectionString?.Invoke(connectionStringBuilder);

        if (configuration.CommandTimeoutSeconds.HasValue)
        {
            connectionStringBuilder.CommandTimeout = configuration.CommandTimeoutSeconds.Value;
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);
        if (configuration.EnableSensitiveLogging)
        {
            dataSourceBuilder.EnableParameterLogging();
        }

        configuration.ConfigureDataSource?.Invoke(dataSourceBuilder);

        _dataSource = dataSourceBuilder.Build();
        _ownsDataSource = true;
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        return _dataSource.OpenConnectionAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _ownsDataSource
            ? _dataSource.DisposeAsync()
            : ValueTask.CompletedTask;
    }
}

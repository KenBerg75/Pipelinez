using Dapper;
using Microsoft.Data.SqlClient;
using Pipelinez.SqlServer.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace Pipelinez.SqlServer.Tests.Infrastructure;

public sealed class SqlServerTestCluster : IAsyncLifetime, IAsyncDisposable
{
    private readonly SqlServerTestClusterOptions _options;
    private readonly MsSqlContainer _container;

    public SqlServerTestCluster()
        : this(SqlServerTestClusterOptions.LoadFromEnvironment())
    {
    }

    internal SqlServerTestCluster(SqlServerTestClusterOptions options)
    {
        _options = options;

        var builder = new MsSqlBuilder(_options.Image)
            .WithPassword(_options.Password);

        if (_options.ReuseContainer)
        {
            builder = builder.WithReuse(true);
        }

        _container = builder.Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(_options.StartupTimeout);
        await _container.StartAsync(cancellation.Token).ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        return ((IAsyncDisposable)this).DisposeAsync().AsTask();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    public SqlServerDestinationOptions CreateDestinationOptions()
    {
        return new SqlServerDestinationOptions
        {
            ConnectionString = ConnectionString
        };
    }

    public SqlServerDeadLetterOptions CreateDeadLetterOptions()
    {
        return new SqlServerDeadLetterOptions
        {
            ConnectionString = ConnectionString
        };
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync(sql).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(string sql, object? parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
    }

    public async Task<T> QuerySingleAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleAsync<T>(sql, parameters).ConfigureAwait(false);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters).ConfigureAwait(false);
    }
}

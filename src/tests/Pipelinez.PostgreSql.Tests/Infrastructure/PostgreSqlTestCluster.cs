using Dapper;
using Npgsql;
using Pipelinez.PostgreSql.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Pipelinez.PostgreSql.Tests.Infrastructure;

public sealed class PostgreSqlTestCluster : IAsyncLifetime, IAsyncDisposable
{
    private readonly PostgreSqlTestClusterOptions _options;
    private readonly PostgreSqlContainer _container;

    public PostgreSqlTestCluster()
        : this(PostgreSqlTestClusterOptions.LoadFromEnvironment())
    {
    }

    internal PostgreSqlTestCluster(PostgreSqlTestClusterOptions options)
    {
        _options = options;

        var builder = new PostgreSqlBuilder(_options.Image)
            .WithDatabase(_options.Database)
            .WithUsername(_options.Username)
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

    public PostgreSqlDestinationOptions CreateDestinationOptions()
    {
        return new PostgreSqlDestinationOptions
        {
            ConnectionString = ConnectionString
        };
    }

    public PostgreSqlDeadLetterOptions CreateDeadLetterOptions()
    {
        return new PostgreSqlDeadLetterOptions
        {
            ConnectionString = ConnectionString
        };
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync(sql).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(string sql, object? parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
    }

    public async Task<T> QuerySingleAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleAsync<T>(sql, parameters).ConfigureAwait(false);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters).ConfigureAwait(false);
    }
}

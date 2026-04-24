using Dapper;
using Npgsql;
using Pipelinez.PostgreSql.Configuration;
using Testcontainers.PostgreSql;

namespace Pipelinez.Benchmarks;

internal sealed class PostgreSqlBenchmarkCluster : IAsyncDisposable
{
    private readonly PostgreSqlBenchmarkClusterOptions _options;
    private readonly PostgreSqlContainer _container;

    public PostgreSqlBenchmarkCluster()
        : this(PostgreSqlBenchmarkClusterOptions.LoadFromEnvironment())
    {
    }

    private PostgreSqlBenchmarkCluster(PostgreSqlBenchmarkClusterOptions options)
    {
        _options = options;

        var builder = new PostgreSqlBuilder(options.Image)
            .WithDatabase(options.Database)
            .WithUsername(options.Username)
            .WithPassword(options.Password);

        if (options.ReuseContainer)
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

    public async Task<T> QuerySingleAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleAsync<T>(sql).ConfigureAwait(false);
    }

    public Task WaitForRowCountAsync(string tableExpression, int expectedCount, TimeSpan timeout)
    {
        return BenchmarkPolling.WaitUntilAsync(
            async () => await QuerySingleAsync<long>($"SELECT COUNT(*) FROM {tableExpression}").ConfigureAwait(false) >= expectedCount,
            timeout,
            $"Timed out waiting for {expectedCount} PostgreSQL rows in {tableExpression}.");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

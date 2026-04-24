using Dapper;
using Microsoft.Data.SqlClient;
using Pipelinez.SqlServer.Configuration;
using Testcontainers.MsSql;

namespace Pipelinez.Benchmarks;

internal sealed class SqlServerBenchmarkCluster : IAsyncDisposable
{
    private readonly SqlServerBenchmarkClusterOptions _options;
    private readonly MsSqlContainer _container;

    public SqlServerBenchmarkCluster()
        : this(SqlServerBenchmarkClusterOptions.LoadFromEnvironment())
    {
    }

    private SqlServerBenchmarkCluster(SqlServerBenchmarkClusterOptions options)
    {
        _options = options;

        var builder = new MsSqlBuilder(options.Image)
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

    public async Task<T> QuerySingleAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleAsync<T>(sql).ConfigureAwait(false);
    }

    public Task WaitForRowCountAsync(string tableExpression, int expectedCount, TimeSpan timeout)
    {
        return BenchmarkPolling.WaitUntilAsync(
            async () => await QuerySingleAsync<long>($"SELECT COUNT(*) FROM {tableExpression}").ConfigureAwait(false) >= expectedCount,
            timeout,
            $"Timed out waiting for {expectedCount} SQL Server rows in {tableExpression}.");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

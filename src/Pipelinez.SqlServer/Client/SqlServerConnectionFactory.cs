using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;
using Pipelinez.SqlServer.Configuration;

namespace Pipelinez.SqlServer.Client;

internal sealed class SqlServerConnectionFactory : ISqlServerConnectionFactory
{
    private readonly string _connectionString;

    internal SqlServerConnectionFactory(SqlServerOptions options)
    {
        var configuration = Guard.Against.Null(options, nameof(options));
        _connectionString = configuration.BuildConnectionString(configuration.GetType().Name);
    }

    public async ValueTask<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

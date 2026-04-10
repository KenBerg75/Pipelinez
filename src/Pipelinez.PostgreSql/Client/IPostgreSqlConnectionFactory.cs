using Npgsql;

namespace Pipelinez.PostgreSql.Client;

internal interface IPostgreSqlConnectionFactory : IAsyncDisposable
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

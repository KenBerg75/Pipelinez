using Microsoft.Data.SqlClient;

namespace Pipelinez.SqlServer.Client;

internal interface ISqlServerConnectionFactory : IAsyncDisposable
{
    ValueTask<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

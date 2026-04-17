using Pipelinez.SqlServer.Mapping;

namespace Pipelinez.SqlServer.Client;

internal interface ISqlServerCommandExecutor
{
    Task ExecuteAsync(SqlServerCommandDefinition command, CancellationToken cancellationToken);
}

using Pipelinez.PostgreSql.Mapping;

namespace Pipelinez.PostgreSql.Client;

internal interface IPostgreSqlCommandExecutor
{
    Task ExecuteAsync(PostgreSqlCommandDefinition command, CancellationToken cancellationToken);
}

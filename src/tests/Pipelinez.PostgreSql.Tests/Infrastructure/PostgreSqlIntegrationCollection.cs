using Xunit;

namespace Pipelinez.PostgreSql.Tests.Infrastructure;

public static class PostgreSqlIntegrationCollection
{
    public const string Name = "PostgreSqlIntegration";
}

[CollectionDefinition(PostgreSqlIntegrationCollection.Name)]
public sealed class PostgreSqlIntegrationCollectionDefinition : ICollectionFixture<PostgreSqlTestCluster>
{
}

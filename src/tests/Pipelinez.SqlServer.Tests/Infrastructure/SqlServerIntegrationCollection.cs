using Xunit;

namespace Pipelinez.SqlServer.Tests.Infrastructure;

public static class SqlServerIntegrationCollection
{
    public const string Name = "SqlServerIntegration";
}

[CollectionDefinition(SqlServerIntegrationCollection.Name)]
public sealed class SqlServerIntegrationCollectionDefinition : ICollectionFixture<SqlServerTestCluster>
{
}

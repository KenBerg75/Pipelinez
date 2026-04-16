using Xunit;

namespace Pipelinez.RabbitMQ.Tests.Infrastructure;

public static class RabbitMqIntegrationCollection
{
    public const string Name = "RabbitMqIntegration";
}

[CollectionDefinition(RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqIntegrationCollectionDefinition : ICollectionFixture<RabbitMqTestCluster>
{
}

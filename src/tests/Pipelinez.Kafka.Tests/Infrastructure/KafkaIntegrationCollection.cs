using Xunit;

namespace Pipelinez.Kafka.Tests.Infrastructure;

public static class KafkaIntegrationCollection
{
    public const string Name = "KafkaIntegration";
}

[CollectionDefinition(KafkaIntegrationCollection.Name)]
public sealed class KafkaIntegrationCollectionDefinition : ICollectionFixture<KafkaTestCluster>
{
}

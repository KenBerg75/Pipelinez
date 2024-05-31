using Pipelinez.Kafka.Client.Admin;
using Pipelinez.Kafka.Client.Consumer;
using Pipelinez.Kafka.Client.Producer;
using Pipelinez.Kafka.Configuration;

namespace Pipelinez.Kafka.Client;

internal static class KafkaClientFactory
{
    internal static IKafkaAdminClient CreateAdminClient(KafkaOptions config)
    {
        return new KafkaAdminClient(config);
    }
    
    internal static IKafkaConsumer<TRecordKey, TRecordValue> CreateConsumer<TRecordKey, TRecordValue>(string pipelineName, KafkaSourceOptions config) where TRecordKey : class where TRecordValue : class
    {
        return new KafkaConsumer<TRecordKey, TRecordValue>(pipelineName, config).Build();
    }
    
    internal static IKafkaProducer<TRecordKey, TRecordValue> CreateProducer<TRecordKey, TRecordValue>(string pipelineName, KafkaDestinationOptions config) where TRecordKey : class where TRecordValue : class
    {
        return new KafkaProducer<TRecordKey, TRecordValue>(pipelineName, config).Build();
    }
}
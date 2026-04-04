using Confluent.Kafka;

namespace Pipelinez.Kafka.Client;

internal interface IKafkaProducer<TRecordKey, TRecordValue>
{
    Task<DeliveryResult<TRecordKey, TRecordValue>> ProduceAsync(
        string topicName,
        Message<TRecordKey, TRecordValue> message,
        CancellationToken cancellationToken);
}

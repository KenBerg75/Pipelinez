using Confluent.Kafka;

namespace Pipelinez.Kafka.Client;

public interface IKafkaProducer<TRecordKey, TRecordValue>
{
    Task<DeliveryResult<TRecordKey, TRecordValue>> ProduceAsync(
        string topicName,
        Message<TRecordKey, TRecordValue> message,
        CancellationToken cancellationToken);
}

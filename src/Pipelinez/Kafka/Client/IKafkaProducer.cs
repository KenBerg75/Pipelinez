using Confluent.Kafka;

namespace Pipelinez.Kafka.Client;

public interface IKafkaProducer<TRecordKey, TRecordValue>
{
    void Produce(string topicName, Message<TRecordKey, TRecordValue> message,
        Action<DeliveryReport<TRecordKey, TRecordValue>> deliveryHandler);
}
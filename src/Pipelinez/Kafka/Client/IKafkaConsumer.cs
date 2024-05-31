using Confluent.Kafka;

namespace Pipelinez.Kafka.Client;

public interface IKafkaConsumer<TMessageKey, TMessageValue>
{
    public string Name { get; }
    public List<string> Subscription { get; }
    public string MemberId { get; }
    
    public void Subscribe(string topicName);
    
    public ConsumeResult<TMessageKey, TMessageValue> Consume(TimeSpan timeout);
    void StoreOffset(TopicPartitionOffset topicPartitionOffset);
}
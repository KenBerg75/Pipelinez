using Confluent.Kafka;

namespace Pipelinez.Kafka.Client;

internal interface IKafkaConsumer<TMessageKey, TMessageValue>
{
    public string Name { get; }
    public List<string> Subscription { get; }
    public string MemberId { get; }
    public event Action<IReadOnlyList<TopicPartition>>? PartitionsAssigned;
    public event Action<IReadOnlyList<TopicPartition>>? PartitionsRevoked;
    
    public void Subscribe(string topicName);
    
    public ConsumeResult<TMessageKey, TMessageValue> Consume(TimeSpan timeout);
    void Pause(IEnumerable<TopicPartition> partitions);
    void Resume(IEnumerable<TopicPartition> partitions);
    void StoreOffset(TopicPartitionOffset topicPartitionOffset);
    void Close();
}

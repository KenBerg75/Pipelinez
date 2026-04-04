using Confluent.Kafka;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Kafka.Source;

internal sealed class KafkaPendingPartitionRecord<T> where T : PipelineRecord
{
    public KafkaPendingPartitionRecord(
        T record,
        MetadataCollection metadata,
        TopicPartitionOffset topicPartitionOffset)
    {
        Record = record;
        Metadata = metadata;
        TopicPartitionOffset = topicPartitionOffset;
    }

    public T Record { get; }

    public MetadataCollection Metadata { get; }

    public TopicPartitionOffset TopicPartitionOffset { get; }
}

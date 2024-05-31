using Confluent.Kafka;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Kafka.Record;

// Not sure this belongs here, but for now....
public static class KafkaMetadataExtensions
{
    /// <summary>
    /// Extracts the metadata collection from the TopicPartitionOffset that is required for transactional support in the pipeline
    /// </summary>
    /// <param name="topicPartitionOffset"></param>
    /// <returns></returns>
    public static MetadataCollection ExtractMetadata(this TopicPartitionOffset topicPartitionOffset)
    {
        var metadata = new MetadataCollection();
        
        // Add the metadata records to support transactional processing
        metadata.Add(new MetadataRecord(KafkaMetadataKeys.SOURCE_TOPIC_NAME, topicPartitionOffset.Topic));
        metadata.Add(new MetadataRecord(KafkaMetadataKeys.SOURCE_PARTITION, Convert.ToString(topicPartitionOffset.Partition.Value)));
        metadata.Add(new MetadataRecord(KafkaMetadataKeys.SOURCE_OFFSET, Convert.ToString(topicPartitionOffset.Offset.Value)));

        return metadata;
    }
}
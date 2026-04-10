using Confluent.Kafka;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Kafka.Record;

// Not sure this belongs here, but for now....
/// <summary>
/// Provides helper methods for converting Kafka transport metadata into Pipelinez metadata structures.
/// </summary>
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
        var partitionKey = BuildPartitionKey(topicPartitionOffset.Topic, topicPartitionOffset.Partition.Value);
        var leaseId = BuildLeaseId(topicPartitionOffset.Topic, topicPartitionOffset.Partition.Value);
        
        // Add the metadata records to support transactional processing
        metadata.Add(new MetadataRecord(KafkaMetadataKeys.SOURCE_TOPIC_NAME, topicPartitionOffset.Topic));
        metadata.Add(new MetadataRecord(KafkaMetadataKeys.SOURCE_PARTITION, Convert.ToString(topicPartitionOffset.Partition.Value)));
        metadata.Add(new MetadataRecord(KafkaMetadataKeys.SOURCE_OFFSET, Convert.ToString(topicPartitionOffset.Offset.Value)));
        metadata.Add(new MetadataRecord(DistributedMetadataKeys.TransportName, "Kafka"));
        metadata.Add(new MetadataRecord(DistributedMetadataKeys.LeaseId, leaseId));
        metadata.Add(new MetadataRecord(DistributedMetadataKeys.PartitionKey, partitionKey));
        metadata.Add(new MetadataRecord(DistributedMetadataKeys.PartitionId, Convert.ToString(topicPartitionOffset.Partition.Value)));
        metadata.Add(new MetadataRecord(DistributedMetadataKeys.Offset, Convert.ToString(topicPartitionOffset.Offset.Value)));

        return metadata;
    }

    /// <summary>
    /// Builds a stable partition key identifier for the specified Kafka topic and partition.
    /// </summary>
    /// <param name="topicName">The Kafka topic name.</param>
    /// <param name="partitionId">The partition identifier.</param>
    /// <returns>A stable partition key value.</returns>
    public static string BuildPartitionKey(string topicName, int partitionId)
    {
        return $"{topicName}:{partitionId}";
    }

    /// <summary>
    /// Builds a stable lease identifier for the specified Kafka topic and partition.
    /// </summary>
    /// <param name="topicName">The Kafka topic name.</param>
    /// <param name="partitionId">The partition identifier.</param>
    /// <returns>A stable lease identifier value.</returns>
    public static string BuildLeaseId(string topicName, int partitionId)
    {
        return $"kafka:{topicName}:{partitionId}";
    }
}

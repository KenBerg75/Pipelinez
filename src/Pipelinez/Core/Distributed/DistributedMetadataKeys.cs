namespace Pipelinez.Core.Distributed;

/// <summary>
/// Defines metadata keys used to store distributed execution information on pipeline containers.
/// </summary>
public static class DistributedMetadataKeys
{
    /// <summary>
    /// The metadata key that stores the transport name.
    /// </summary>
    public const string TransportName = "pipelinez.distribution.transport.name";
    /// <summary>
    /// The metadata key that stores the logical lease identifier.
    /// </summary>
    public const string LeaseId = "pipelinez.distribution.lease.id";
    /// <summary>
    /// The metadata key that stores the logical partition key.
    /// </summary>
    public const string PartitionKey = "pipelinez.distribution.partition.key";
    /// <summary>
    /// The metadata key that stores the numeric partition identifier.
    /// </summary>
    public const string PartitionId = "pipelinez.distribution.partition.id";
    /// <summary>
    /// The metadata key that stores the transport offset.
    /// </summary>
    public const string Offset = "pipelinez.distribution.offset";
}

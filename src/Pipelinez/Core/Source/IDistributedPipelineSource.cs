using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Source;

/// <summary>
/// Defines a source that can report distributed ownership and partition execution state.
/// </summary>
/// <typeparam name="T">The pipeline record type produced by the source.</typeparam>
public interface IDistributedPipelineSource<T> : IPipelineSource<T> where T : PipelineRecord
{
    /// <summary>
    /// Gets a value indicating whether the source supports distributed execution.
    /// </summary>
    bool SupportsDistributedExecution { get; }

    /// <summary>
    /// Gets the transport name reported in distributed metadata.
    /// </summary>
    string TransportName { get; }

    /// <summary>
    /// Gets the partitions currently owned by the source.
    /// </summary>
    /// <returns>The owned partition leases.</returns>
    IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions();

    /// <summary>
    /// Gets the current execution state for owned partitions.
    /// </summary>
    /// <returns>The partition execution state snapshots.</returns>
    IReadOnlyList<PipelinePartitionExecutionState> GetPartitionExecutionStates();
}

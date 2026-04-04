using Pipelinez.Core.Distributed;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Source;

public interface IDistributedPipelineSource<T> : IPipelineSource<T> where T : PipelineRecord
{
    bool SupportsDistributedExecution { get; }

    string TransportName { get; }

    IReadOnlyList<PipelinePartitionLease> GetOwnedPartitions();

    IReadOnlyList<PipelinePartitionExecutionState> GetPartitionExecutionStates();
}

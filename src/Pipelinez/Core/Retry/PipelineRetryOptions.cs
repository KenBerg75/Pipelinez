using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

public sealed class PipelineRetryOptions<T> where T : PipelineRecord
{
    public PipelineRetryPolicy<T>? DefaultSegmentPolicy { get; init; }

    public PipelineRetryPolicy<T>? DestinationPolicy { get; init; }

    public bool EmitRetryEvents { get; init; } = true;
}

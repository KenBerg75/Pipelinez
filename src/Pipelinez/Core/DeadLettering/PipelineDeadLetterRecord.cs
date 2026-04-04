using Ardalis.GuardClauses;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Retry;

namespace Pipelinez.Core.DeadLettering;

public sealed class PipelineDeadLetterRecord<T> where T : PipelineRecord
{
    public required T Record { get; init; }

    public required PipelineFaultState Fault { get; init; }

    public required MetadataCollection Metadata { get; init; }

    public required IReadOnlyList<PipelineSegmentExecution> SegmentHistory { get; init; }

    public required IReadOnlyList<PipelineRetryAttempt> RetryHistory { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset DeadLetteredAtUtc { get; init; }

    public required PipelineRecordDistributionContext Distribution { get; init; }

    public void Validate()
    {
        Guard.Against.Null(Record, nameof(Record));
        Guard.Against.Null(Fault, nameof(Fault));
        Guard.Against.Null(Metadata, nameof(Metadata));
        Guard.Against.Null(SegmentHistory, nameof(SegmentHistory));
        Guard.Against.Null(RetryHistory, nameof(RetryHistory));
        Guard.Against.Null(Distribution, nameof(Distribution));
    }
}

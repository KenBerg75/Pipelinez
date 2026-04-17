using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Retry;

namespace Pipelinez.AmazonS3.DeadLettering;

internal sealed class AmazonS3DeadLetterEnvelope<T>
    where T : PipelineRecord
{
    public required T Record { get; init; }

    public required AmazonS3DeadLetterFault Fault { get; init; }

    public required IReadOnlyDictionary<string, string> Metadata { get; init; }

    public required IReadOnlyList<PipelineSegmentExecution> SegmentHistory { get; init; }

    public required IReadOnlyList<PipelineRetryAttempt> RetryHistory { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset DeadLetteredAtUtc { get; init; }

    public required PipelineRecordDistributionContext Distribution { get; init; }
}

internal sealed class AmazonS3DeadLetterFault
{
    public required string ComponentName { get; init; }

    public required string ComponentKind { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public string? Message { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }
}

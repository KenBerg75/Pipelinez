using Ardalis.GuardClauses;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Retry;

namespace Pipelinez.Core.DeadLettering;

/// <summary>
/// Represents the full dead-letter envelope persisted for a terminally handled pipeline record.
/// </summary>
/// <typeparam name="T">The pipeline record type captured in the dead-letter envelope.</typeparam>
public sealed class PipelineDeadLetterRecord<T> where T : PipelineRecord
{
    /// <summary>
    /// Gets the original record that was dead-lettered.
    /// </summary>
    public required T Record { get; init; }

    /// <summary>
    /// Gets the fault state that caused the record to be dead-lettered.
    /// </summary>
    public required PipelineFaultState Fault { get; init; }

    /// <summary>
    /// Gets the metadata associated with the record at dead-letter time.
    /// </summary>
    public required MetadataCollection Metadata { get; init; }

    /// <summary>
    /// Gets the execution history recorded for pipeline segments that processed the record.
    /// </summary>
    public required IReadOnlyList<PipelineSegmentExecution> SegmentHistory { get; init; }

    /// <summary>
    /// Gets the retry history recorded for the record.
    /// </summary>
    public required IReadOnlyList<PipelineRetryAttempt> RetryHistory { get; init; }

    /// <summary>
    /// Gets the timestamp when the container carrying the record was created.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets the timestamp when the record was written to the dead-letter destination.
    /// </summary>
    public required DateTimeOffset DeadLetteredAtUtc { get; init; }

    /// <summary>
    /// Gets the distributed execution context associated with the record.
    /// </summary>
    public required PipelineRecordDistributionContext Distribution { get; init; }

    /// <summary>
    /// Validates that the dead-letter envelope contains the required values.
    /// </summary>
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

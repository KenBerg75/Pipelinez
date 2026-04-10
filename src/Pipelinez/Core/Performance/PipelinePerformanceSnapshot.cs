namespace Pipelinez.Core.Performance;

/// <summary>
/// Represents an aggregated performance snapshot for a running pipeline.
/// </summary>
public sealed class PipelinePerformanceSnapshot
{
    /// <summary>
    /// Initializes a new pipeline performance snapshot.
    /// </summary>
    public PipelinePerformanceSnapshot(
        DateTimeOffset startedAtUtc,
        TimeSpan elapsed,
        long totalRecordsPublished,
        long totalRecordsCompleted,
        long totalRecordsFaulted,
        long totalRetryCount,
        long successfulRetryRecoveries,
        long retryExhaustions,
        long totalDeadLetteredCount,
        long totalDeadLetterFailureCount,
        long totalPublishWaitCount,
        TimeSpan averagePublishWaitDuration,
        long totalPublishRejectedCount,
        int peakBufferedCount,
        double recordsPerSecond,
        TimeSpan averageEndToEndLatency,
        IReadOnlyList<PipelineComponentPerformanceSnapshot> components)
    {
        StartedAtUtc = startedAtUtc;
        Elapsed = elapsed;
        TotalRecordsPublished = totalRecordsPublished;
        TotalRecordsCompleted = totalRecordsCompleted;
        TotalRecordsFaulted = totalRecordsFaulted;
        TotalRetryCount = totalRetryCount;
        SuccessfulRetryRecoveries = successfulRetryRecoveries;
        RetryExhaustions = retryExhaustions;
        TotalDeadLetteredCount = totalDeadLetteredCount;
        TotalDeadLetterFailureCount = totalDeadLetterFailureCount;
        TotalPublishWaitCount = totalPublishWaitCount;
        AveragePublishWaitDuration = averagePublishWaitDuration;
        TotalPublishRejectedCount = totalPublishRejectedCount;
        PeakBufferedCount = peakBufferedCount;
        RecordsPerSecond = recordsPerSecond;
        AverageEndToEndLatency = averageEndToEndLatency;
        Components = components;
    }

    /// <summary>
    /// Gets the time performance collection started.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// Gets the elapsed time covered by the snapshot.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets the total number of records published into the pipeline.
    /// </summary>
    public long TotalRecordsPublished { get; }

    /// <summary>
    /// Gets the total number of records completed successfully.
    /// </summary>
    public long TotalRecordsCompleted { get; }

    /// <summary>
    /// Gets the total number of faulted records observed by the pipeline.
    /// </summary>
    public long TotalRecordsFaulted { get; }

    /// <summary>
    /// Gets the total number of retry attempts performed.
    /// </summary>
    public long TotalRetryCount { get; }

    /// <summary>
    /// Gets the number of times retry recovered a record successfully.
    /// </summary>
    public long SuccessfulRetryRecoveries { get; }

    /// <summary>
    /// Gets the number of times retries were exhausted.
    /// </summary>
    public long RetryExhaustions { get; }

    /// <summary>
    /// Gets the total number of dead-lettered records.
    /// </summary>
    public long TotalDeadLetteredCount { get; }

    /// <summary>
    /// Gets the total number of dead-letter write failures.
    /// </summary>
    public long TotalDeadLetterFailureCount { get; }

    /// <summary>
    /// Gets the total number of publish calls that waited for capacity.
    /// </summary>
    public long TotalPublishWaitCount { get; }

    /// <summary>
    /// Gets the average duration spent waiting for capacity during publish.
    /// </summary>
    public TimeSpan AveragePublishWaitDuration { get; }

    /// <summary>
    /// Gets the total number of rejected publish calls.
    /// </summary>
    public long TotalPublishRejectedCount { get; }

    /// <summary>
    /// Gets the peak buffered record count observed across the pipeline.
    /// </summary>
    public int PeakBufferedCount { get; }

    /// <summary>
    /// Gets the current calculated records-per-second rate for the pipeline.
    /// </summary>
    public double RecordsPerSecond { get; }

    /// <summary>
    /// Gets the average end-to-end latency for completed records.
    /// </summary>
    public TimeSpan AverageEndToEndLatency { get; }

    /// <summary>
    /// Gets the component-level performance snapshots.
    /// </summary>
    public IReadOnlyList<PipelineComponentPerformanceSnapshot> Components { get; }
}

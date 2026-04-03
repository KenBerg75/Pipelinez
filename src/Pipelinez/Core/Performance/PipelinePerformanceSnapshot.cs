namespace Pipelinez.Core.Performance;

public sealed class PipelinePerformanceSnapshot
{
    public PipelinePerformanceSnapshot(
        DateTimeOffset startedAtUtc,
        TimeSpan elapsed,
        long totalRecordsPublished,
        long totalRecordsCompleted,
        long totalRecordsFaulted,
        long totalRetryCount,
        long successfulRetryRecoveries,
        long retryExhaustions,
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
        RecordsPerSecond = recordsPerSecond;
        AverageEndToEndLatency = averageEndToEndLatency;
        Components = components;
    }

    public DateTimeOffset StartedAtUtc { get; }

    public TimeSpan Elapsed { get; }

    public long TotalRecordsPublished { get; }

    public long TotalRecordsCompleted { get; }

    public long TotalRecordsFaulted { get; }

    public long TotalRetryCount { get; }

    public long SuccessfulRetryRecoveries { get; }

    public long RetryExhaustions { get; }

    public double RecordsPerSecond { get; }

    public TimeSpan AverageEndToEndLatency { get; }

    public IReadOnlyList<PipelineComponentPerformanceSnapshot> Components { get; }
}

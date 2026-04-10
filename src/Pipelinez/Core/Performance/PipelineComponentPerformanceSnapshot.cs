namespace Pipelinez.Core.Performance;

/// <summary>
/// Represents performance statistics for a single pipeline component.
/// </summary>
public sealed class PipelineComponentPerformanceSnapshot
{
    /// <summary>
    /// Initializes a new component performance snapshot.
    /// </summary>
    public PipelineComponentPerformanceSnapshot(
        string componentName,
        long processedCount,
        long faultedCount,
        double recordsPerSecond,
        TimeSpan averageExecutionLatency,
        int? queueDepth = null)
    {
        ComponentName = componentName;
        ProcessedCount = processedCount;
        FaultedCount = faultedCount;
        RecordsPerSecond = recordsPerSecond;
        AverageExecutionLatency = averageExecutionLatency;
        QueueDepth = queueDepth;
    }

    /// <summary>
    /// Gets the component name.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Gets the number of records processed successfully by the component.
    /// </summary>
    public long ProcessedCount { get; }

    /// <summary>
    /// Gets the number of faulted executions observed for the component.
    /// </summary>
    public long FaultedCount { get; }

    /// <summary>
    /// Gets the current calculated records-per-second rate for the component.
    /// </summary>
    public double RecordsPerSecond { get; }

    /// <summary>
    /// Gets the average execution latency for the component.
    /// </summary>
    public TimeSpan AverageExecutionLatency { get; }

    /// <summary>
    /// Gets the current queue depth, if available.
    /// </summary>
    public int? QueueDepth { get; }
}

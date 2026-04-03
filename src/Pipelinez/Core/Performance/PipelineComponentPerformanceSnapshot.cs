namespace Pipelinez.Core.Performance;

public sealed class PipelineComponentPerformanceSnapshot
{
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

    public string ComponentName { get; }

    public long ProcessedCount { get; }

    public long FaultedCount { get; }

    public double RecordsPerSecond { get; }

    public TimeSpan AverageExecutionLatency { get; }

    public int? QueueDepth { get; }
}

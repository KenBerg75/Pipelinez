namespace Pipelinez.Core.Performance;

public sealed class PipelineMetricsOptions
{
    public bool EnableRuntimeMetrics { get; init; } = true;

    public bool EnablePerComponentTiming { get; init; } = true;
}

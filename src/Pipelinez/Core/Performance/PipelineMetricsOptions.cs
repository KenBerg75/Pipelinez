namespace Pipelinez.Core.Performance;

/// <summary>
/// Configures which runtime metrics are emitted by the pipeline performance collector.
/// </summary>
public sealed class PipelineMetricsOptions
{
    /// <summary>
    /// Gets a value indicating whether aggregate runtime metrics should be emitted.
    /// </summary>
    public bool EnableRuntimeMetrics { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether per-component timing metrics should be emitted.
    /// </summary>
    public bool EnablePerComponentTiming { get; init; } = true;
}

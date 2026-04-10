namespace Pipelinez.Core.Performance;

/// <summary>
/// Configures performance-related execution settings for the pipeline.
/// </summary>
public sealed class PipelinePerformanceOptions
{
    /// <summary>
    /// Gets the execution options applied to the source.
    /// </summary>
    public PipelineExecutionOptions SourceExecution { get; init; } =
        PipelineExecutionOptions.CreateDefaultSourceOptions();

    /// <summary>
    /// Gets the default execution options applied to segments.
    /// </summary>
    public PipelineExecutionOptions DefaultSegmentExecution { get; init; } =
        PipelineExecutionOptions.CreateDefaultSegmentOptions();

    /// <summary>
    /// Gets the execution options applied to the destination.
    /// </summary>
    public PipelineExecutionOptions DestinationExecution { get; init; } =
        PipelineExecutionOptions.CreateDefaultDestinationOptions();

    /// <summary>
    /// Gets the batching options applied to the destination when it supports batching.
    /// </summary>
    public PipelineBatchingOptions? DestinationBatching { get; init; }

    /// <summary>
    /// Gets the metrics collection options for performance instrumentation.
    /// </summary>
    public PipelineMetricsOptions Metrics { get; init; } = new();

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PipelinePerformanceOptions Validate()
    {
        SourceExecution.Validate();
        DefaultSegmentExecution.Validate();
        DestinationExecution.Validate();
        DestinationBatching?.Validate();
        return this;
    }
}

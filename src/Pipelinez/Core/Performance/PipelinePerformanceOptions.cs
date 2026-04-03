namespace Pipelinez.Core.Performance;

public sealed class PipelinePerformanceOptions
{
    public PipelineExecutionOptions SourceExecution { get; init; } =
        PipelineExecutionOptions.CreateDefaultSourceOptions();

    public PipelineExecutionOptions DefaultSegmentExecution { get; init; } =
        PipelineExecutionOptions.CreateDefaultSegmentOptions();

    public PipelineExecutionOptions DestinationExecution { get; init; } =
        PipelineExecutionOptions.CreateDefaultDestinationOptions();

    public PipelineBatchingOptions? DestinationBatching { get; init; }

    public PipelineMetricsOptions Metrics { get; init; } = new();

    public PipelinePerformanceOptions Validate()
    {
        SourceExecution.Validate();
        DefaultSegmentExecution.Validate();
        DestinationExecution.Validate();
        DestinationBatching?.Validate();
        return this;
    }
}

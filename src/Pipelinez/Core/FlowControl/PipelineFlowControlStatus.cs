namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Represents the current flow-control state of the pipeline as a whole.
/// </summary>
public sealed class PipelineFlowControlStatus
{
    /// <summary>
    /// Initializes a new flow-control snapshot.
    /// </summary>
    public PipelineFlowControlStatus(
        PipelineOverflowPolicy overflowPolicy,
        bool isSaturated,
        double saturationRatio,
        int totalBufferedCount,
        int? totalCapacity,
        IReadOnlyList<PipelineComponentFlowStatus> components)
    {
        OverflowPolicy = overflowPolicy;
        IsSaturated = isSaturated;
        SaturationRatio = saturationRatio;
        TotalBufferedCount = totalBufferedCount;
        TotalCapacity = totalCapacity;
        Components = components;
    }

    /// <summary>
    /// Gets the default overflow policy applied by the pipeline.
    /// </summary>
    public PipelineOverflowPolicy OverflowPolicy { get; }

    /// <summary>
    /// Gets a value indicating whether any component is currently saturated.
    /// </summary>
    public bool IsSaturated { get; }

    /// <summary>
    /// Gets the overall saturation ratio across bounded components.
    /// </summary>
    public double SaturationRatio { get; }

    /// <summary>
    /// Gets the total buffered record count across observed components.
    /// </summary>
    public int TotalBufferedCount { get; }

    /// <summary>
    /// Gets the total bounded capacity across observed components, if one exists.
    /// </summary>
    public int? TotalCapacity { get; }

    /// <summary>
    /// Gets the component-level flow snapshots.
    /// </summary>
    public IReadOnlyList<PipelineComponentFlowStatus> Components { get; }
}

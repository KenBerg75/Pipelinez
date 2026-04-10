namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Describes the flow-control state of a single pipeline component.
/// </summary>
public sealed class PipelineComponentFlowStatus
{
    /// <summary>
    /// Initializes a new component flow-control snapshot.
    /// </summary>
    public PipelineComponentFlowStatus(
        string name,
        int approximateQueueDepth,
        int? boundedCapacity)
    {
        Name = name;
        ApproximateQueueDepth = approximateQueueDepth;
        BoundedCapacity = boundedCapacity;
    }

    /// <summary>
    /// Gets the component name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the approximate number of buffered items for the component.
    /// </summary>
    public int ApproximateQueueDepth { get; }

    /// <summary>
    /// Gets the component's bounded capacity, if one is configured.
    /// </summary>
    public int? BoundedCapacity { get; }

    /// <summary>
    /// Gets a value indicating whether the component is currently saturated.
    /// </summary>
    public bool IsSaturated => BoundedCapacity.HasValue && ApproximateQueueDepth >= BoundedCapacity.Value;

    /// <summary>
    /// Gets the saturation ratio for the component when a bounded capacity exists.
    /// </summary>
    public double SaturationRatio => BoundedCapacity.HasValue && BoundedCapacity.Value > 0
        ? Math.Min(1d, (double)ApproximateQueueDepth / BoundedCapacity.Value)
        : 0d;
}

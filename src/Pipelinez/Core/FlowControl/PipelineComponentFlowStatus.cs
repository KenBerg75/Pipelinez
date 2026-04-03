namespace Pipelinez.Core.FlowControl;

public sealed class PipelineComponentFlowStatus
{
    public PipelineComponentFlowStatus(
        string name,
        int approximateQueueDepth,
        int? boundedCapacity)
    {
        Name = name;
        ApproximateQueueDepth = approximateQueueDepth;
        BoundedCapacity = boundedCapacity;
    }

    public string Name { get; }

    public int ApproximateQueueDepth { get; }

    public int? BoundedCapacity { get; }

    public bool IsSaturated => BoundedCapacity.HasValue && ApproximateQueueDepth >= BoundedCapacity.Value;

    public double SaturationRatio => BoundedCapacity.HasValue && BoundedCapacity.Value > 0
        ? Math.Min(1d, (double)ApproximateQueueDepth / BoundedCapacity.Value)
        : 0d;
}

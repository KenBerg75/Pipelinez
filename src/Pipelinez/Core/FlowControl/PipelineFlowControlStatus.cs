namespace Pipelinez.Core.FlowControl;

public sealed class PipelineFlowControlStatus
{
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

    public PipelineOverflowPolicy OverflowPolicy { get; }

    public bool IsSaturated { get; }

    public double SaturationRatio { get; }

    public int TotalBufferedCount { get; }

    public int? TotalCapacity { get; }

    public IReadOnlyList<PipelineComponentFlowStatus> Components { get; }
}

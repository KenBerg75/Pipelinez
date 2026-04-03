using Ardalis.GuardClauses;

namespace Pipelinez.Core.FlowControl;

public sealed class PipelineFlowControlOptions
{
    public PipelineOverflowPolicy OverflowPolicy { get; init; } = PipelineOverflowPolicy.Wait;

    public TimeSpan? PublishTimeout { get; init; }

    public bool EmitSaturationEvents { get; init; } = true;

    public double SaturationWarningThreshold { get; init; } = 0.80d;

    public PipelineFlowControlOptions Validate()
    {
        if (PublishTimeout.HasValue && PublishTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PublishTimeout),
                PublishTimeout,
                "Publish timeout must be greater than zero when provided.");
        }

        Guard.Against.OutOfRange(
            SaturationWarningThreshold,
            nameof(SaturationWarningThreshold),
            0d,
            1d);

        return this;
    }
}

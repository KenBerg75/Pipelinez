using Ardalis.GuardClauses;

namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Configures pipeline-wide flow-control behavior.
/// </summary>
public sealed class PipelineFlowControlOptions
{
    /// <summary>
    /// Gets the default overflow policy used when publication encounters saturation.
    /// </summary>
    public PipelineOverflowPolicy OverflowPolicy { get; init; } = PipelineOverflowPolicy.Wait;

    /// <summary>
    /// Gets the default timeout used while waiting for capacity.
    /// </summary>
    public TimeSpan? PublishTimeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether saturation change events should be emitted.
    /// </summary>
    public bool EmitSaturationEvents { get; init; } = true;

    /// <summary>
    /// Gets the saturation ratio threshold that should be treated as a warning level.
    /// </summary>
    public double SaturationWarningThreshold { get; init; } = 0.80d;

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
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

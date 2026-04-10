namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Specifies per-call flow-control behavior for a publish request.
/// </summary>
public sealed class PipelinePublishOptions
{
    /// <summary>
    /// Gets the maximum time to wait for capacity before the publish attempt times out.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets the cancellation token used to cancel the publish request while waiting for capacity.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Gets the overflow policy override applied to this publish request.
    /// </summary>
    public PipelineOverflowPolicy? OverflowPolicyOverride { get; init; }

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PipelinePublishOptions Validate()
    {
        if (Timeout.HasValue && Timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Timeout),
                Timeout,
                "Publish timeout must be greater than zero when provided.");
        }

        return this;
    }
}

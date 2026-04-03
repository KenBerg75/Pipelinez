namespace Pipelinez.Core.FlowControl;

public sealed class PipelinePublishOptions
{
    public TimeSpan? Timeout { get; init; }

    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    public PipelineOverflowPolicy? OverflowPolicyOverride { get; init; }

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

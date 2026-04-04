using Ardalis.GuardClauses;

namespace Pipelinez.Core.Operational;

public sealed class PipelineOperationalOptions
{
    public bool EnableHealthChecks { get; init; } = true;

    public bool EnableMetrics { get; init; } = true;

    public bool EnableCorrelationIds { get; init; } = true;

    public bool IncludeComponentDiagnosticsInHealthResults { get; init; } = true;

    public double SaturationDegradedThreshold { get; init; } = 0.90d;

    public long RetryExhaustionDegradedThreshold { get; init; } = 1;

    public long DeadLetterDegradedThreshold { get; init; } = 1;

    public long PublishRejectionDegradedThreshold { get; init; } = 1;

    public int DrainingPartitionDegradedThreshold { get; init; } = 1;

    public PipelineOperationalOptions Validate()
    {
        Guard.Against.OutOfRange(SaturationDegradedThreshold, nameof(SaturationDegradedThreshold), 0d, 1d);
        Guard.Against.Negative(RetryExhaustionDegradedThreshold, nameof(RetryExhaustionDegradedThreshold));
        Guard.Against.Negative(DeadLetterDegradedThreshold, nameof(DeadLetterDegradedThreshold));
        Guard.Against.Negative(PublishRejectionDegradedThreshold, nameof(PublishRejectionDegradedThreshold));
        Guard.Against.Negative(DrainingPartitionDegradedThreshold, nameof(DrainingPartitionDegradedThreshold));
        return this;
    }
}

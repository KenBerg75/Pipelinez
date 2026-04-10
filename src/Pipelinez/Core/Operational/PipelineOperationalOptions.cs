using Ardalis.GuardClauses;

namespace Pipelinez.Core.Operational;

/// <summary>
/// Configures health, diagnostics, and metrics behavior for the pipeline runtime.
/// </summary>
public sealed class PipelineOperationalOptions
{
    /// <summary>
    /// Gets a value indicating whether health checks are enabled.
    /// </summary>
    public bool EnableHealthChecks { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether metrics emission is enabled.
    /// </summary>
    public bool EnableMetrics { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether correlation identifiers should be added to records.
    /// </summary>
    public bool EnableCorrelationIds { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether component details should be included in health-check results.
    /// </summary>
    public bool IncludeComponentDiagnosticsInHealthResults { get; init; } = true;

    /// <summary>
    /// Gets the saturation ratio at which the pipeline should be considered degraded.
    /// </summary>
    public double SaturationDegradedThreshold { get; init; } = 0.90d;

    /// <summary>
    /// Gets the retry exhaustion count at which the pipeline should be considered degraded.
    /// </summary>
    public long RetryExhaustionDegradedThreshold { get; init; } = 1;

    /// <summary>
    /// Gets the dead-letter count at which the pipeline should be considered degraded.
    /// </summary>
    public long DeadLetterDegradedThreshold { get; init; } = 1;

    /// <summary>
    /// Gets the publish rejection count at which the pipeline should be considered degraded.
    /// </summary>
    public long PublishRejectionDegradedThreshold { get; init; } = 1;

    /// <summary>
    /// Gets the number of draining partitions at which the pipeline should be considered degraded.
    /// </summary>
    public int DrainingPartitionDegradedThreshold { get; init; } = 1;

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
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

namespace Pipelinez.Core.DeadLettering;

/// <summary>
/// Configures how the pipeline handles dead-letter writes and dead-letter events.
/// </summary>
public sealed class PipelineDeadLetterOptions
{
    /// <summary>
    /// Gets a value indicating whether dead-letter success and failure events should be emitted.
    /// </summary>
    public bool EmitDeadLetterEvents { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a dead-letter write failure should fault the pipeline.
    /// </summary>
    public bool TreatDeadLetterFailureAsPipelineFault { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether metadata should be cloned when dead-letter envelopes are created.
    /// </summary>
    public bool CloneMetadata { get; init; } = true;

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PipelineDeadLetterOptions Validate()
    {
        return this;
    }
}

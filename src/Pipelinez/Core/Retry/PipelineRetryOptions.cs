using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

/// <summary>
/// Configures default retry behavior for pipeline segments and destinations.
/// </summary>
/// <typeparam name="T">The pipeline record type processed by the retry policies.</typeparam>
public sealed class PipelineRetryOptions<T> where T : PipelineRecord
{
    /// <summary>
    /// Gets the default retry policy applied to segments when no per-segment override is configured.
    /// </summary>
    public PipelineRetryPolicy<T>? DefaultSegmentPolicy { get; init; }

    /// <summary>
    /// Gets the retry policy applied to the destination when no destination override is configured.
    /// </summary>
    public PipelineRetryPolicy<T>? DestinationPolicy { get; init; }

    /// <summary>
    /// Gets a value indicating whether retry events should be emitted.
    /// </summary>
    public bool EmitRetryEvents { get; init; } = true;
}

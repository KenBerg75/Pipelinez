using Ardalis.GuardClauses;

namespace Pipelinez.Core.Performance;

/// <summary>
/// Configures batch execution behavior for destinations that support batching.
/// </summary>
public sealed class PipelineBatchingOptions
{
    /// <summary>
    /// Gets the maximum number of records included in a batch.
    /// </summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Gets the maximum amount of time to wait before flushing a partial batch.
    /// </summary>
    public TimeSpan MaxBatchDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <returns>The validated options instance.</returns>
    public PipelineBatchingOptions Validate()
    {
        Guard.Against.NegativeOrZero(BatchSize, nameof(BatchSize));

        if (MaxBatchDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxBatchDelay),
                MaxBatchDelay,
                "Max batch delay must be zero or greater.");
        }

        return this;
    }
}

using Ardalis.GuardClauses;

namespace Pipelinez.Core.Performance;

public sealed class PipelineBatchingOptions
{
    public int BatchSize { get; init; } = 100;

    public TimeSpan MaxBatchDelay { get; init; } = TimeSpan.FromMilliseconds(100);

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

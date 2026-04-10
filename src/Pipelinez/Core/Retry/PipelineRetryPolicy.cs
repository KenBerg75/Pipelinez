using Ardalis.GuardClauses;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

/// <summary>
/// Defines retry behavior for transient failures in segments or destinations.
/// </summary>
/// <typeparam name="T">The pipeline record type processed by the policy.</typeparam>
public sealed class PipelineRetryPolicy<T> where T : PipelineRecord
{
    private readonly IReadOnlyList<Func<PipelineRetryContext<T>, bool>> _filters;

    private PipelineRetryPolicy(
        int maxAttempts,
        Func<PipelineRetryContext<T>, TimeSpan> delayProvider,
        IReadOnlyList<Func<PipelineRetryContext<T>, bool>> filters)
    {
        MaxAttempts = Guard.Against.NegativeOrZero(maxAttempts, nameof(maxAttempts));
        DelayProvider = Guard.Against.Null(delayProvider, nameof(delayProvider));
        _filters = Guard.Against.Null(filters, nameof(filters));
    }

    /// <summary>
    /// Gets the maximum number of attempts allowed by the policy.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the delegate used to calculate delay before the next attempt.
    /// </summary>
    public Func<PipelineRetryContext<T>, TimeSpan> DelayProvider { get; }

    /// <summary>
    /// Creates a policy that performs no retries beyond the initial attempt.
    /// </summary>
    /// <returns>A retry policy with no retry behavior.</returns>
    public static PipelineRetryPolicy<T> None()
    {
        return new PipelineRetryPolicy<T>(
            1,
            _ => TimeSpan.Zero,
            Array.Empty<Func<PipelineRetryContext<T>, bool>>());
    }

    /// <summary>
    /// Creates a fixed-delay retry policy.
    /// </summary>
    /// <param name="maxAttempts">The total number of attempts, including the initial attempt.</param>
    /// <param name="delay">The delay applied between attempts.</param>
    /// <returns>A fixed-delay retry policy.</returns>
    public static PipelineRetryPolicy<T> FixedDelay(int maxAttempts, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Retry delay must be zero or greater.");
        }

        return new PipelineRetryPolicy<T>(
            maxAttempts,
            _ => delay,
            Array.Empty<Func<PipelineRetryContext<T>, bool>>());
    }

    /// <summary>
    /// Creates an exponential backoff retry policy.
    /// </summary>
    /// <param name="maxAttempts">The total number of attempts, including the initial attempt.</param>
    /// <param name="initialDelay">The delay used before the first retry attempt.</param>
    /// <param name="maxDelay">The maximum delay allowed between attempts.</param>
    /// <param name="useJitter">A value indicating whether randomized jitter should be applied.</param>
    /// <returns>An exponential backoff retry policy.</returns>
    public static PipelineRetryPolicy<T> ExponentialBackoff(
        int maxAttempts,
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        bool useJitter = false)
    {
        if (initialDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialDelay),
                initialDelay,
                "Initial retry delay must be zero or greater.");
        }

        if (maxDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDelay),
                maxDelay,
                "Maximum retry delay must be zero or greater.");
        }

        if (maxDelay < initialDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDelay),
                maxDelay,
                "Maximum retry delay must be greater than or equal to the initial delay.");
        }

        return new PipelineRetryPolicy<T>(
            maxAttempts,
            context =>
            {
                var multiplier = Math.Pow(2, Math.Max(context.AttemptNumber - 1, 0));
                var unboundedMilliseconds = initialDelay.TotalMilliseconds * multiplier;
                var cappedMilliseconds = Math.Min(unboundedMilliseconds, maxDelay.TotalMilliseconds);

                if (!useJitter)
                {
                    return TimeSpan.FromMilliseconds(cappedMilliseconds);
                }

                return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * cappedMilliseconds);
            },
            Array.Empty<Func<PipelineRetryContext<T>, bool>>());
    }

    /// <summary>
    /// Restricts retries to exceptions of the specified type.
    /// </summary>
    /// <typeparam name="TException">The exception type that should be retried.</typeparam>
    /// <returns>A new retry policy with the additional filter applied.</returns>
    public PipelineRetryPolicy<T> Handle<TException>() where TException : Exception
    {
        return Handle(context => context.Exception is TException);
    }

    /// <summary>
    /// Restricts retries using a custom predicate.
    /// </summary>
    /// <param name="predicate">The predicate used to determine whether a retry is allowed.</param>
    /// <returns>A new retry policy with the additional filter applied.</returns>
    public PipelineRetryPolicy<T> Handle(Func<PipelineRetryContext<T>, bool> predicate)
    {
        Guard.Against.Null(predicate, nameof(predicate));

        return new PipelineRetryPolicy<T>(
            MaxAttempts,
            DelayProvider,
            _filters.Concat(new[] { predicate }).ToArray());
    }

    internal bool CanRetry(PipelineRetryContext<T> context)
    {
        Guard.Against.Null(context, nameof(context));

        if (context.AttemptNumber >= MaxAttempts)
        {
            return false;
        }

        if (_filters.Count == 0)
        {
            return true;
        }

        return _filters.Any(filter => filter(context));
    }
}

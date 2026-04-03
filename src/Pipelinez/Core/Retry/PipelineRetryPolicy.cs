using Ardalis.GuardClauses;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

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

    public int MaxAttempts { get; }

    public Func<PipelineRetryContext<T>, TimeSpan> DelayProvider { get; }

    public static PipelineRetryPolicy<T> None()
    {
        return new PipelineRetryPolicy<T>(
            1,
            _ => TimeSpan.Zero,
            Array.Empty<Func<PipelineRetryContext<T>, bool>>());
    }

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

    public PipelineRetryPolicy<T> Handle<TException>() where TException : Exception
    {
        return Handle(context => context.Exception is TException);
    }

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

using System.Diagnostics;
using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

internal static class PipelineRetryExecutor
{
    public static async Task<TResult> ExecuteAsync<T, TResult>(
        PipelineContainer<T> container,
        PipelineRetryPolicy<T>? retryPolicy,
        string componentName,
        PipelineComponentKind componentKind,
        Func<CancellationToken> cancellationTokenProvider,
        Func<PipelineRetryAttempt, Exception, Task> onRetryingAsync,
        Func<Task> onRetryRecoveredAsync,
        Func<CancellationToken, Task<TResult>> operation)
        where T : PipelineRecord
    {
        Guard.Against.Null(container, nameof(container));
        Guard.Against.NullOrWhiteSpace(componentName, nameof(componentName));
        Guard.Against.Null(cancellationTokenProvider, nameof(cancellationTokenProvider));
        Guard.Against.Null(onRetryingAsync, nameof(onRetryingAsync));
        Guard.Against.Null(onRetryRecoveredAsync, nameof(onRetryRecoveredAsync));
        Guard.Against.Null(operation, nameof(operation));

        var effectivePolicy = retryPolicy ?? PipelineRetryPolicy<T>.None();

        for (var attemptNumber = 1; ; attemptNumber++)
        {
            var cancellationToken = cancellationTokenProvider();
            var startedAtUtc = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await operation(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                if (attemptNumber > 1)
                {
                    await onRetryRecoveredAsync().ConfigureAwait(false);
                }

                return result;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                stopwatch.Stop();

                var retryContext = new PipelineRetryContext<T>(
                    exception,
                    container,
                    componentName,
                    componentKind,
                    attemptNumber,
                    effectivePolicy.MaxAttempts,
                    cancellationToken);

                if (!effectivePolicy.CanRetry(retryContext))
                {
                    throw;
                }

                var delay = effectivePolicy.DelayProvider(retryContext);
                if (delay < TimeSpan.Zero)
                {
                    throw new InvalidOperationException(
                        $"Retry delay for component '{componentName}' cannot be negative.");
                }

                var retryAttempt = new PipelineRetryAttempt(
                    attemptNumber,
                    componentName,
                    componentKind,
                    startedAtUtc,
                    startedAtUtc.Add(stopwatch.Elapsed),
                    stopwatch.Elapsed,
                    delay,
                    exception.GetType().Name,
                    exception.Message);

                await onRetryingAsync(retryAttempt, exception).ConfigureAwait(false);

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}

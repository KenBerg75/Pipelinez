using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

/// <summary>
/// Provides context for retry policy evaluation.
/// </summary>
/// <typeparam name="T">The pipeline record type being retried.</typeparam>
public sealed class PipelineRetryContext<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new retry context.
    /// </summary>
    public PipelineRetryContext(
        Exception exception,
        PipelineContainer<T> container,
        string componentName,
        PipelineComponentKind componentKind,
        int attemptNumber,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        Exception = Guard.Against.Null(exception, nameof(exception));
        Container = Guard.Against.Null(container, nameof(container));
        ComponentName = Guard.Against.NullOrWhiteSpace(componentName, nameof(componentName));
        ComponentKind = componentKind;
        AttemptNumber = Guard.Against.NegativeOrZero(attemptNumber, nameof(attemptNumber));
        MaxAttempts = Guard.Against.NegativeOrZero(maxAttempts, nameof(maxAttempts));
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the exception that triggered the retry evaluation.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the pipeline container being retried.
    /// </summary>
    public PipelineContainer<T> Container { get; }

    /// <summary>
    /// Gets the record being retried.
    /// </summary>
    public T Record => Container.Record;

    /// <summary>
    /// Gets the component name associated with the retry.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Gets the component kind associated with the retry.
    /// </summary>
    public PipelineComponentKind ComponentKind { get; }

    /// <summary>
    /// Gets the current attempt number, starting at one.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the maximum number of attempts allowed by the retry policy.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the cancellation token associated with the retry operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}

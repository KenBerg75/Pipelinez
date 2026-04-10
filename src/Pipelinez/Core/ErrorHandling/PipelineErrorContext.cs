using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Retry;

namespace Pipelinez.Core.ErrorHandling;

/// <summary>
/// Provides the context passed to a configured pipeline error handler.
/// </summary>
/// <typeparam name="T">The pipeline record type being handled.</typeparam>
public sealed class PipelineErrorContext<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new error handler context.
    /// </summary>
    public PipelineErrorContext(
        Exception exception,
        PipelineContainer<T> container,
        PipelineFaultState fault,
        CancellationToken cancellationToken)
    {
        Exception = Guard.Against.Null(exception, nameof(exception));
        Container = Guard.Against.Null(container, nameof(container));
        Fault = Guard.Against.Null(fault, nameof(fault));
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the exception that triggered error handling.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the faulted pipeline container.
    /// </summary>
    public PipelineContainer<T> Container { get; }

    /// <summary>
    /// Gets the faulted record.
    /// </summary>
    public T Record => Container.Record;

    /// <summary>
    /// Gets the recorded fault state.
    /// </summary>
    public PipelineFaultState Fault { get; }

    /// <summary>
    /// Gets the component name associated with the fault.
    /// </summary>
    public string ComponentName => Fault.ComponentName;

    /// <summary>
    /// Gets the component kind associated with the fault.
    /// </summary>
    public PipelineComponentKind ComponentKind => Fault.ComponentKind;

    /// <summary>
    /// Gets the runtime cancellation token associated with the pipeline.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the retry history recorded for the container.
    /// </summary>
    public IReadOnlyList<PipelineRetryAttempt> RetryHistory => Container.RetryHistory.ToArray();

    /// <summary>
    /// Gets the number of recorded retry attempts.
    /// </summary>
    public int RetryAttemptCount => Container.RetryHistory.Count;

    /// <summary>
    /// Gets a value indicating whether the record exhausted at least one retry attempt.
    /// </summary>
    public bool RetryExhausted => RetryAttemptCount > 0;
}

using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

public sealed class PipelineRetryContext<T> where T : PipelineRecord
{
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

    public Exception Exception { get; }

    public PipelineContainer<T> Container { get; }

    public T Record => Container.Record;

    public string ComponentName { get; }

    public PipelineComponentKind ComponentKind { get; }

    public int AttemptNumber { get; }

    public int MaxAttempts { get; }

    public CancellationToken CancellationToken { get; }
}

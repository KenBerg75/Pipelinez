using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.ErrorHandling;

public sealed class PipelineErrorContext<T> where T : PipelineRecord
{
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

    public Exception Exception { get; }

    public PipelineContainer<T> Container { get; }

    public T Record => Container.Record;

    public PipelineFaultState Fault { get; }

    public string ComponentName => Fault.ComponentName;

    public PipelineComponentKind ComponentKind => Fault.ComponentKind;

    public CancellationToken CancellationToken { get; }
}

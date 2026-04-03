using Ardalis.GuardClauses;

namespace Pipelinez.Core.FaultHandling;

public sealed class PipelineFaultState
{
    public PipelineFaultState(
        Exception exception,
        string componentName,
        PipelineComponentKind componentKind,
        DateTimeOffset occurredAtUtc,
        string? message = null)
    {
        Exception = Guard.Against.Null(exception, nameof(exception));
        ComponentName = Guard.Against.NullOrWhiteSpace(componentName, nameof(componentName));
        ComponentKind = componentKind;
        OccurredAtUtc = occurredAtUtc;
        Message = message;
    }

    public Exception Exception { get; }

    public string ComponentName { get; }

    public PipelineComponentKind ComponentKind { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public string? Message { get; }
}

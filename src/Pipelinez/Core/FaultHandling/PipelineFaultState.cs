using Ardalis.GuardClauses;

namespace Pipelinez.Core.FaultHandling;

/// <summary>
/// Represents a fault captured by Pipelinez for a record or the pipeline runtime.
/// </summary>
public sealed class PipelineFaultState
{
    /// <summary>
    /// Initializes a new fault state instance.
    /// </summary>
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

    /// <summary>
    /// Gets the underlying exception that caused the fault.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the logical component name that produced the fault.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Gets the kind of component that produced the fault.
    /// </summary>
    public PipelineComponentKind ComponentKind { get; }

    /// <summary>
    /// Gets the time the fault occurred.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// Gets the consumer-facing message associated with the fault.
    /// </summary>
    public string? Message { get; }
}

using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;

namespace Pipelinez.Core.Retry;

/// <summary>
/// Describes a single retry attempt performed for a transient failure.
/// </summary>
public sealed class PipelineRetryAttempt
{
    /// <summary>
    /// Initializes a new retry attempt record.
    /// </summary>
    public PipelineRetryAttempt(
        int attemptNumber,
        string componentName,
        PipelineComponentKind componentKind,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        TimeSpan delayBeforeNextAttempt,
        string exceptionType,
        string message)
    {
        AttemptNumber = Guard.Against.NegativeOrZero(attemptNumber, nameof(attemptNumber));
        ComponentName = Guard.Against.NullOrWhiteSpace(componentName, nameof(componentName));
        ComponentKind = componentKind;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Duration = duration;
        DelayBeforeNextAttempt = delayBeforeNextAttempt;
        ExceptionType = Guard.Against.NullOrWhiteSpace(exceptionType, nameof(exceptionType));
        Message = Guard.Against.NullOrWhiteSpace(message, nameof(message));
    }

    /// <summary>
    /// Gets the retry attempt number, starting at one.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the component name associated with the retry.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Gets the component kind associated with the retry.
    /// </summary>
    public PipelineComponentKind ComponentKind { get; }

    /// <summary>
    /// Gets the time the attempt started.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// Gets the time the attempt completed.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// Gets the duration of the attempt.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the configured delay before the next retry attempt.
    /// </summary>
    public TimeSpan DelayBeforeNextAttempt { get; }

    /// <summary>
    /// Gets the exception type name captured for the attempt.
    /// </summary>
    public string ExceptionType { get; }

    /// <summary>
    /// Gets the exception message captured for the attempt.
    /// </summary>
    public string Message { get; }
}

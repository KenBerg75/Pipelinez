using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;

namespace Pipelinez.Core.Retry;

public sealed class PipelineRetryAttempt
{
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

    public int AttemptNumber { get; }

    public string ComponentName { get; }

    public PipelineComponentKind ComponentKind { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public TimeSpan Duration { get; }

    public TimeSpan DelayBeforeNextAttempt { get; }

    public string ExceptionType { get; }

    public string Message { get; }
}

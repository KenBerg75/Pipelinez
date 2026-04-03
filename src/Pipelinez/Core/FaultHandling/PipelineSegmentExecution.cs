using Ardalis.GuardClauses;

namespace Pipelinez.Core.FaultHandling;

public sealed class PipelineSegmentExecution
{
    public PipelineSegmentExecution(
        string segmentName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool succeeded,
        string? failureMessage = null)
    {
        SegmentName = Guard.Against.NullOrWhiteSpace(segmentName, nameof(segmentName));
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Succeeded = succeeded;
        FailureMessage = failureMessage;
    }

    public string SegmentName { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;

    public bool Succeeded { get; }

    public string? FailureMessage { get; }
}

using Ardalis.GuardClauses;

namespace Pipelinez.Core.FaultHandling;

/// <summary>
/// Records the execution outcome for a pipeline segment.
/// </summary>
public sealed class PipelineSegmentExecution
{
    /// <summary>
    /// Initializes a new segment execution record.
    /// </summary>
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

    /// <summary>
    /// Gets the segment name.
    /// </summary>
    public string SegmentName { get; }

    /// <summary>
    /// Gets the time segment execution started.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// Gets the time segment execution completed.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// Gets the total execution duration for the segment.
    /// </summary>
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;

    /// <summary>
    /// Gets a value indicating whether the segment completed successfully.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the failure message recorded for the segment, if execution failed.
    /// </summary>
    public string? FailureMessage { get; }
}

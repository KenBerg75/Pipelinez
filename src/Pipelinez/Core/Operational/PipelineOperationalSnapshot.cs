using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Status;

namespace Pipelinez.Core.Operational;

/// <summary>
/// Represents an operator-focused runtime snapshot for a pipeline.
/// </summary>
public sealed class PipelineOperationalSnapshot
{
    /// <summary>
    /// Initializes a new operational snapshot.
    /// </summary>
    public PipelineOperationalSnapshot(
        PipelineStatus status,
        PipelinePerformanceSnapshot performance,
        PipelineHealthStatus health,
        DateTimeOffset observedAtUtc,
        PipelineFaultState? lastPipelineFault = null,
        DateTimeOffset? lastRecordCompletedAtUtc = null,
        DateTimeOffset? lastDeadLetteredAtUtc = null)
    {
        Status = Guard.Against.Null(status, nameof(status));
        Performance = Guard.Against.Null(performance, nameof(performance));
        Health = Guard.Against.Null(health, nameof(health));
        ObservedAtUtc = observedAtUtc;
        LastPipelineFault = lastPipelineFault;
        LastRecordCompletedAtUtc = lastRecordCompletedAtUtc;
        LastDeadLetteredAtUtc = lastDeadLetteredAtUtc;
    }

    /// <summary>
    /// Gets the runtime status snapshot.
    /// </summary>
    public PipelineStatus Status { get; }

    /// <summary>
    /// Gets the performance snapshot.
    /// </summary>
    public PipelinePerformanceSnapshot Performance { get; }

    /// <summary>
    /// Gets the health snapshot.
    /// </summary>
    public PipelineHealthStatus Health { get; }

    /// <summary>
    /// Gets the time the snapshot was observed.
    /// </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>
    /// Gets the last pipeline-level fault, if one exists.
    /// </summary>
    public PipelineFaultState? LastPipelineFault { get; }

    /// <summary>
    /// Gets the last successful record completion time, if one exists.
    /// </summary>
    public DateTimeOffset? LastRecordCompletedAtUtc { get; }

    /// <summary>
    /// Gets the last dead-lettered record time, if one exists.
    /// </summary>
    public DateTimeOffset? LastDeadLetteredAtUtc { get; }
}

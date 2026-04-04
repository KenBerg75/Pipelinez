using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Status;

namespace Pipelinez.Core.Operational;

public sealed class PipelineOperationalSnapshot
{
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

    public PipelineStatus Status { get; }

    public PipelinePerformanceSnapshot Performance { get; }

    public PipelineHealthStatus Health { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public PipelineFaultState? LastPipelineFault { get; }

    public DateTimeOffset? LastRecordCompletedAtUtc { get; }

    public DateTimeOffset? LastDeadLetteredAtUtc { get; }
}

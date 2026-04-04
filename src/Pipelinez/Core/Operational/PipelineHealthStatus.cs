using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Status;

namespace Pipelinez.Core.Operational;

public sealed class PipelineHealthStatus
{
    public PipelineHealthStatus(
        string pipelineName,
        PipelineHealthState state,
        IReadOnlyList<string> reasons,
        DateTimeOffset observedAtUtc,
        PipelineStatus runtimeStatus,
        PipelinePerformanceSnapshot performance,
        PipelineFaultState? lastPipelineFault = null)
    {
        PipelineName = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        State = state;
        Reasons = Guard.Against.Null(reasons, nameof(reasons)).ToArray();
        ObservedAtUtc = observedAtUtc;
        RuntimeStatus = Guard.Against.Null(runtimeStatus, nameof(runtimeStatus));
        Performance = Guard.Against.Null(performance, nameof(performance));
        LastPipelineFault = lastPipelineFault;
    }

    public string PipelineName { get; }

    public PipelineHealthState State { get; }

    public IReadOnlyList<string> Reasons { get; }

    public string? Reason => Reasons.Count == 0 ? null : string.Join("; ", Reasons);

    public DateTimeOffset ObservedAtUtc { get; }

    public PipelineStatus RuntimeStatus { get; }

    public PipelinePerformanceSnapshot Performance { get; }

    public PipelineFaultState? LastPipelineFault { get; }
}

using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Status;

namespace Pipelinez.Core.Operational;

/// <summary>
/// Represents a health snapshot for a pipeline.
/// </summary>
public sealed class PipelineHealthStatus
{
    /// <summary>
    /// Initializes a new health status snapshot.
    /// </summary>
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

    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// Gets the current health state.
    /// </summary>
    public PipelineHealthState State { get; }

    /// <summary>
    /// Gets the list of reasons contributing to the health state.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; }

    /// <summary>
    /// Gets a single formatted health reason string, when available.
    /// </summary>
    public string? Reason => Reasons.Count == 0 ? null : string.Join("; ", Reasons);

    /// <summary>
    /// Gets the time the health snapshot was observed.
    /// </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>
    /// Gets the runtime status snapshot used to evaluate health.
    /// </summary>
    public PipelineStatus RuntimeStatus { get; }

    /// <summary>
    /// Gets the performance snapshot used to evaluate health.
    /// </summary>
    public PipelinePerformanceSnapshot Performance { get; }

    /// <summary>
    /// Gets the last pipeline-level fault, if one has occurred.
    /// </summary>
    public PipelineFaultState? LastPipelineFault { get; }
}

namespace Pipelinez.Core.Operational;

/// <summary>
/// Represents the current health state of a pipeline.
/// </summary>
public enum PipelineHealthState
{
    /// <summary>
    /// The pipeline is starting up and has not yet reached steady-state execution.
    /// </summary>
    Starting = 0,
    /// <summary>
    /// The pipeline is operating normally.
    /// </summary>
    Healthy = 1,
    /// <summary>
    /// The pipeline is operating but has exceeded one or more degraded thresholds.
    /// </summary>
    Degraded = 2,
    /// <summary>
    /// The pipeline is unhealthy or faulted.
    /// </summary>
    Unhealthy = 3,
    /// <summary>
    /// The pipeline completed successfully.
    /// </summary>
    Completed = 4
}

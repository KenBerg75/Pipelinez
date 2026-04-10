namespace Pipelinez.Core.Distributed;

/// <summary>
/// Represents the execution mode used by a pipeline host.
/// </summary>
public enum PipelineExecutionMode
{
    /// <summary>
    /// Executes the pipeline within a single host process without transport ownership coordination.
    /// </summary>
    SingleProcess,
    /// <summary>
    /// Executes the pipeline in distributed mode with transport-specific ownership coordination.
    /// </summary>
    Distributed
}

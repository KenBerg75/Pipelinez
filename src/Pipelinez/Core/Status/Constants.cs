namespace Pipelinez.Core.Status;

/// <summary>
/// Represents high-level execution states reported for pipeline components and runtimes.
/// </summary>
public enum PipelineExecutionStatus
{
    /// <summary>
    /// Indicates the component or runtime is operating normally.
    /// </summary>
    Healthy,
    /// <summary>
    /// Indicates the component or runtime has completed successfully.
    /// </summary>
    Completed,
    /// <summary>
    /// Indicates the component or runtime has faulted.
    /// </summary>
    Faulted,
    /// <summary>
    /// Indicates Pipelinez could not determine a single status from the available information.
    /// </summary>
    Unknown
}

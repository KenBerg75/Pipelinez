using Pipelinez.Core.FlowControl;

namespace Pipelinez.Core.Status;

/// <summary>
/// Describes the current status of a single pipeline component.
/// </summary>
public class PipelineComponentStatus
{
    /// <summary>
    /// Initializes a new component status snapshot.
    /// </summary>
    /// <param name="name">The logical component name.</param>
    /// <param name="status">The component execution status.</param>
    /// <param name="flow">Optional flow-control information for the component.</param>
    public PipelineComponentStatus(
        string name,
        PipelineExecutionStatus status,
        PipelineComponentFlowStatus? flow = null)
    {
        Name = name;
        Status = status;
        Flow = flow;
    }

    /// <summary>
    /// Gets the component name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the current execution status of the component.
    /// </summary>
    public PipelineExecutionStatus Status { get; }
    /// <summary>
    /// Gets the current flow-control information for the component, if available.
    /// </summary>
    public PipelineComponentFlowStatus? Flow { get; }
}

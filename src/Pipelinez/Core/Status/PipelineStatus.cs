using Pipelinez.Core.Distributed;
using Pipelinez.Core.FlowControl;

namespace Pipelinez.Core.Status;

/// <summary>
/// Represents the current observable runtime status of a pipeline.
/// </summary>
public class PipelineStatus
{
    /// <summary>
    /// Initializes a new pipeline status snapshot.
    /// </summary>
    /// <param name="components">The component-level status entries.</param>
    /// <param name="runtimeStatus">The explicit runtime status, if one is available.</param>
    /// <param name="distributedStatus">The distributed execution status, if applicable.</param>
    /// <param name="flowControlStatus">The flow-control status, if applicable.</param>
    public PipelineStatus(
        IList<PipelineComponentStatus> components,
        PipelineExecutionStatus? runtimeStatus = null,
        PipelineDistributedStatus? distributedStatus = null,
        PipelineFlowControlStatus? flowControlStatus = null)
    {
        Components = components;
        RuntimeStatus = runtimeStatus;
        DistributedStatus = distributedStatus;
        FlowControlStatus = flowControlStatus;
    }
    
    /// <summary>
    /// Gets the overall pipeline status derived from the runtime status and component states.
    /// </summary>
    public PipelineExecutionStatus Status => GetStatus();
    /// <summary>
    /// Gets the component-level status entries.
    /// </summary>
    public IList<PipelineComponentStatus> Components { get; }
    /// <summary>
    /// Gets the explicit runtime status, if one is available.
    /// </summary>
    public PipelineExecutionStatus? RuntimeStatus { get; }
    /// <summary>
    /// Gets distributed runtime information, if the pipeline is running in distributed mode.
    /// </summary>
    public PipelineDistributedStatus? DistributedStatus { get; }
    /// <summary>
    /// Gets flow-control information for the pipeline, if available.
    /// </summary>
    public PipelineFlowControlStatus? FlowControlStatus { get; }
    
    private PipelineExecutionStatus GetStatus()
    {
        if (RuntimeStatus.HasValue)
        {
            return RuntimeStatus.Value;
        }

        if (Components.Any(c => c.Status == PipelineExecutionStatus.Faulted))
        { return PipelineExecutionStatus.Faulted; }
        if (Components.All(c => c.Status == PipelineExecutionStatus.Completed))
        { return PipelineExecutionStatus.Completed; }
        if (Components.All(c => c.Status == PipelineExecutionStatus.Healthy))
        { return PipelineExecutionStatus.Healthy; }
        return PipelineExecutionStatus.Unknown;
    }
} 

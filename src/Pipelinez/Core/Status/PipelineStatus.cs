using Pipelinez.Core.Distributed;
using Pipelinez.Core.FlowControl;

namespace Pipelinez.Core.Status;

public class PipelineStatus
{
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
    
    public PipelineExecutionStatus Status => GetStatus();
    public IList<PipelineComponentStatus> Components { get; }
    public PipelineExecutionStatus? RuntimeStatus { get; }
    public PipelineDistributedStatus? DistributedStatus { get; }
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

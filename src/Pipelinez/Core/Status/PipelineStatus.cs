using Pipelinez.Core.Distributed;

namespace Pipelinez.Core.Status;

public class PipelineStatus
{
    public PipelineStatus(
        IList<PipelineComponentStatus> components,
        PipelineExecutionStatus? runtimeStatus = null,
        PipelineDistributedStatus? distributedStatus = null)
    {
        Components = components;
        RuntimeStatus = runtimeStatus;
        DistributedStatus = distributedStatus;
    }
    
    public PipelineExecutionStatus Status => GetStatus();
    public IList<PipelineComponentStatus> Components { get; }
    public PipelineExecutionStatus? RuntimeStatus { get; }
    public PipelineDistributedStatus? DistributedStatus { get; }
    
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

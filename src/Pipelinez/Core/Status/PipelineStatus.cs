namespace Pipelinez.Core.Status;

public class PipelineStatus
{
    public PipelineStatus(IList<PipelineComponentStatus> components)
    {
        Components = components;
    }
    
    public PipelineExecutionStatus Status => GetStatus();
    public IList<PipelineComponentStatus> Components { get; }
    
    private PipelineExecutionStatus GetStatus()
    {
        if (Components.Any(c => c.Status == PipelineExecutionStatus.Faulted))
        { return PipelineExecutionStatus.Faulted; }
        if (Components.All(c => c.Status == PipelineExecutionStatus.Completed))
        { return PipelineExecutionStatus.Completed; }
        if (Components.All(c => c.Status == PipelineExecutionStatus.Healthy))
        { return PipelineExecutionStatus.Healthy; }
        return PipelineExecutionStatus.Unknown;
    }
} 
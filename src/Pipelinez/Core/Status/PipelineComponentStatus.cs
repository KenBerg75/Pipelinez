namespace Pipelinez.Core.Status;

public class PipelineComponentStatus
{
    public PipelineComponentStatus(string name, PipelineExecutionStatus status)
    {
        Name = name;
        Status = status;
    }

    public string Name { get; }
    public PipelineExecutionStatus Status { get; }
}
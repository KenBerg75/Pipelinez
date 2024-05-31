namespace Pipelinez.Core.Status;

public static class Extensions
{
    
    internal static PipelineExecutionStatus ToPipelineExecutionStatus(this TaskStatus status)
    {
        switch(status)
        {
            case TaskStatus.Created:
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
                return PipelineExecutionStatus.Healthy;
            case TaskStatus.RanToCompletion:
                return PipelineExecutionStatus.Completed;
            case TaskStatus.Canceled:
            case TaskStatus.Faulted:
                return PipelineExecutionStatus.Faulted;
            default:
                return PipelineExecutionStatus.Unknown;
        }
    }
}
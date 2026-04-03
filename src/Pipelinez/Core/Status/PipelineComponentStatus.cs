using Pipelinez.Core.FlowControl;

namespace Pipelinez.Core.Status;

public class PipelineComponentStatus
{
    public PipelineComponentStatus(
        string name,
        PipelineExecutionStatus status,
        PipelineComponentFlowStatus? flow = null)
    {
        Name = name;
        Status = status;
        Flow = flow;
    }

    public string Name { get; }
    public PipelineExecutionStatus Status { get; }
    public PipelineComponentFlowStatus? Flow { get; }
}

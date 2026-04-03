namespace Pipelinez.Core.FlowControl;

public enum PipelinePublishResultReason
{
    Accepted,
    RejectedByOverflowPolicy,
    TimedOut,
    Canceled
}

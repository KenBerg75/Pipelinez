namespace Pipelinez.Core.FlowControl;

internal interface IPipelineFlowStatusProvider
{
    int GetApproximateQueueDepth();

    int? GetBoundedCapacity();
}

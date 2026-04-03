namespace Pipelinez.Core.Distributed;

public sealed class PipelineHostOptions
{
    public PipelineExecutionMode ExecutionMode { get; init; } = PipelineExecutionMode.SingleProcess;

    public string? InstanceId { get; init; }

    public string? WorkerId { get; init; }

    public bool RequireTransportOwnership { get; init; } = true;
}

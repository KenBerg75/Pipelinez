namespace Pipelinez.Core.Distributed;

/// <summary>
/// Configures host-level execution behavior for a pipeline.
/// </summary>
public sealed class PipelineHostOptions
{
    /// <summary>
    /// Gets the execution mode used by the pipeline.
    /// </summary>
    public PipelineExecutionMode ExecutionMode { get; init; } = PipelineExecutionMode.SingleProcess;

    /// <summary>
    /// Gets the host instance identifier reported in runtime and diagnostic data.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// Gets the worker identifier reported in runtime and diagnostic data.
    /// </summary>
    public string? WorkerId { get; init; }

    /// <summary>
    /// Gets a value indicating whether distributed mode requires transport ownership support.
    /// </summary>
    public bool RequireTransportOwnership { get; init; } = true;
}

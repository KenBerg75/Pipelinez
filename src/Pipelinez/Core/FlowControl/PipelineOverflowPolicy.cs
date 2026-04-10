namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Determines how publication behaves when the pipeline is saturated.
/// </summary>
public enum PipelineOverflowPolicy
{
    /// <summary>
    /// Wait for capacity to become available.
    /// </summary>
    Wait,
    /// <summary>
    /// Reject the publish request immediately.
    /// </summary>
    Reject,
    /// <summary>
    /// Cancel the publish request if it would need to wait for capacity.
    /// </summary>
    Cancel
}

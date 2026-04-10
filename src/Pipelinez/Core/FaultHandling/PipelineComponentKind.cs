namespace Pipelinez.Core.FaultHandling;

/// <summary>
/// Identifies the pipeline component that originated a fault.
/// </summary>
public enum PipelineComponentKind
{
    /// <summary>
    /// The fault originated in the source component.
    /// </summary>
    Source,
    /// <summary>
    /// The fault originated in a segment component.
    /// </summary>
    Segment,
    /// <summary>
    /// The fault originated in the destination component.
    /// </summary>
    Destination,
    /// <summary>
    /// The fault originated in pipeline orchestration code.
    /// </summary>
    Pipeline
}

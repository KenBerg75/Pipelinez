namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Describes the outcome of a publish attempt.
/// </summary>
public enum PipelinePublishResultReason
{
    /// <summary>
    /// The record was accepted by the pipeline.
    /// </summary>
    Accepted,
    /// <summary>
    /// The record was rejected because the configured overflow policy refused publication.
    /// </summary>
    RejectedByOverflowPolicy,
    /// <summary>
    /// The publish request timed out before capacity became available.
    /// </summary>
    TimedOut,
    /// <summary>
    /// The publish request was canceled before capacity became available.
    /// </summary>
    Canceled
}

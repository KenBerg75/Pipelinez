using System.Runtime.ExceptionServices;

namespace Pipelinez.Core.FlowControl;

/// <summary>
/// Represents the result of attempting to publish a record into the pipeline.
/// </summary>
public sealed class PipelinePublishResult
{
    private PipelinePublishResult(
        bool accepted,
        PipelinePublishResultReason reason,
        TimeSpan waitDuration)
    {
        Accepted = accepted;
        Reason = reason;
        WaitDuration = waitDuration;
    }

    /// <summary>
    /// Gets a value indicating whether the record was accepted by the pipeline.
    /// </summary>
    public bool Accepted { get; }

    /// <summary>
    /// Gets the reason associated with the publish outcome.
    /// </summary>
    public PipelinePublishResultReason Reason { get; }

    /// <summary>
    /// Gets the amount of time spent waiting for capacity before the outcome was produced.
    /// </summary>
    public TimeSpan WaitDuration { get; }

    /// <summary>
    /// Creates a successful publish result.
    /// </summary>
    /// <param name="waitDuration">The time spent waiting for capacity.</param>
    /// <returns>A successful publish result.</returns>
    public static PipelinePublishResult AcceptedResult(TimeSpan waitDuration)
    {
        return new PipelinePublishResult(true, PipelinePublishResultReason.Accepted, waitDuration);
    }

    /// <summary>
    /// Creates a rejected publish result.
    /// </summary>
    /// <param name="reason">The rejection reason.</param>
    /// <param name="waitDuration">The time spent waiting before rejection.</param>
    /// <returns>A rejected publish result.</returns>
    public static PipelinePublishResult Rejected(PipelinePublishResultReason reason, TimeSpan waitDuration)
    {
        if (reason == PipelinePublishResultReason.Accepted)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Rejected results cannot use Accepted reason.");
        }

        return new PipelinePublishResult(false, reason, waitDuration);
    }

    /// <summary>
    /// Throws an exception when the publish attempt was not accepted.
    /// </summary>
    public void ThrowIfNotAccepted()
    {
        if (Accepted)
        {
            return;
        }

        switch (Reason)
        {
            case PipelinePublishResultReason.RejectedByOverflowPolicy:
                throw new InvalidOperationException("The record could not be published because the pipeline is saturated and rejected the publish request.");
            case PipelinePublishResultReason.TimedOut:
                throw new TimeoutException("The record could not be published before the configured flow-control timeout elapsed.");
            case PipelinePublishResultReason.Canceled:
                ExceptionDispatchInfo.Capture(new OperationCanceledException("The record publish operation was canceled before capacity became available.")).Throw();
                break;
            default:
                throw new InvalidOperationException($"Unknown publish result reason '{Reason}'.");
        }
    }
}

using System.Runtime.ExceptionServices;

namespace Pipelinez.Core.FlowControl;

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

    public bool Accepted { get; }

    public PipelinePublishResultReason Reason { get; }

    public TimeSpan WaitDuration { get; }

    public static PipelinePublishResult AcceptedResult(TimeSpan waitDuration)
    {
        return new PipelinePublishResult(true, PipelinePublishResultReason.Accepted, waitDuration);
    }

    public static PipelinePublishResult Rejected(PipelinePublishResultReason reason, TimeSpan waitDuration)
    {
        if (reason == PipelinePublishResultReason.Accepted)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Rejected results cannot use Accepted reason.");
        }

        return new PipelinePublishResult(false, reason, waitDuration);
    }

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

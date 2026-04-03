using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.FlowControl;

internal static class PipelineFlowController
{
    public static async Task<PipelinePublishResult> PublishAsync<T>(
        BufferBlock<PipelineContainer<T>> target,
        PipelineContainer<T> container,
        PipelineFlowControlOptions flowControlOptions,
        PipelinePublishOptions publishOptions,
        CancellationToken runtimeCancellationToken)
        where T : PipelineRecord
    {
        if (target.Post(container))
        {
            return PipelinePublishResult.AcceptedResult(TimeSpan.Zero);
        }

        var overflowPolicy = publishOptions.OverflowPolicyOverride ?? flowControlOptions.OverflowPolicy;
        var timeout = publishOptions.Timeout ?? flowControlOptions.PublishTimeout;

        if (overflowPolicy == PipelineOverflowPolicy.Reject)
        {
            return PipelinePublishResult.Rejected(PipelinePublishResultReason.RejectedByOverflowPolicy, TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeoutCancellation = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : null;
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            GetTokens(runtimeCancellationToken, publishOptions, overflowPolicy, timeoutCancellation));

        try
        {
            var accepted = await target.SendAsync(container, linkedCancellation.Token).ConfigureAwait(false);
            stopwatch.Stop();

            return accepted
                ? PipelinePublishResult.AcceptedResult(stopwatch.Elapsed)
                : PipelinePublishResult.Rejected(PipelinePublishResultReason.RejectedByOverflowPolicy, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            if (timeoutCancellation is { IsCancellationRequested: true })
            {
                return PipelinePublishResult.Rejected(PipelinePublishResultReason.TimedOut, stopwatch.Elapsed);
            }

            return PipelinePublishResult.Rejected(PipelinePublishResultReason.Canceled, stopwatch.Elapsed);
        }
    }

    private static CancellationToken[] GetTokens(
        CancellationToken runtimeCancellationToken,
        PipelinePublishOptions publishOptions,
        PipelineOverflowPolicy overflowPolicy,
        CancellationTokenSource? timeoutCancellation)
    {
        var tokens = new List<CancellationToken>(3)
        {
            runtimeCancellationToken
        };

        if (overflowPolicy == PipelineOverflowPolicy.Cancel && publishOptions.CancellationToken.CanBeCanceled)
        {
            tokens.Add(publishOptions.CancellationToken);
        }

        if (timeoutCancellation is not null)
        {
            tokens.Add(timeoutCancellation.Token);
        }

        return tokens.ToArray();
    }
}

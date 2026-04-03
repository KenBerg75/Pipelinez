using Pipelinez.Core;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Status;
using Pipelinez.Tests.Core.DestinationTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.FlowControlTests;

public sealed class PipelineFlowControlTests
{
    [Fact]
    public async Task Pipeline_Publish_With_RejectPolicy_Returns_Rejected_Result_When_Saturated()
    {
        var (pipeline, destination) = CreatePipeline(
            "flow-reject",
            new PipelineFlowControlOptions
            {
                OverflowPolicy = PipelineOverflowPolicy.Wait
            });

        var rejectedEvents = new List<PipelinePublishRejectedEventArgs<TestPipelineRecord>>();
        pipeline.OnPublishRejected += (_, args) => rejectedEvents.Add(args);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await SaturatePipelineAsync(pipeline, destination).ConfigureAwait(false);

        var result = await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "rejected" },
                new PipelinePublishOptions
                {
                    OverflowPolicyOverride = PipelineOverflowPolicy.Reject
                })
            .ConfigureAwait(false);

        await DrainPipelineAsync(pipeline, destination).ConfigureAwait(false);

        Assert.False(result.Accepted);
        Assert.Equal(PipelinePublishResultReason.RejectedByOverflowPolicy, result.Reason);
        Assert.Single(rejectedEvents);
        Assert.Equal("rejected", rejectedEvents[0].Record.Data);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
        Assert.Equal(1, pipeline.GetPerformanceSnapshot().TotalPublishRejectedCount);
    }

    [Fact]
    public async Task Pipeline_Publish_With_Timeout_Returns_TimedOut_Result_When_Saturated()
    {
        var (pipeline, destination) = CreatePipeline(
            "flow-timeout",
            new PipelineFlowControlOptions
            {
                OverflowPolicy = PipelineOverflowPolicy.Wait,
                PublishTimeout = TimeSpan.FromMilliseconds(200)
            });

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await SaturatePipelineAsync(pipeline, destination).ConfigureAwait(false);

        var startedAt = DateTimeOffset.UtcNow;
        var result = await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "timeout" },
                new PipelinePublishOptions())
            .ConfigureAwait(false);
        var elapsed = DateTimeOffset.UtcNow - startedAt;

        await DrainPipelineAsync(pipeline, destination).ConfigureAwait(false);

        Assert.False(result.Accepted);
        Assert.Equal(PipelinePublishResultReason.TimedOut, result.Reason);
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(150));

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.True(snapshot.TotalPublishWaitCount >= 1);
        Assert.True(snapshot.AveragePublishWaitDuration > TimeSpan.Zero);
        Assert.True(snapshot.TotalPublishRejectedCount >= 1);
    }

    [Fact]
    public async Task Pipeline_Publish_With_CancelPolicy_Returns_Canceled_Result_When_Saturated()
    {
        var (pipeline, destination) = CreatePipeline(
            "flow-cancel",
            new PipelineFlowControlOptions
            {
                OverflowPolicy = PipelineOverflowPolicy.Wait
            });

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await SaturatePipelineAsync(pipeline, destination).ConfigureAwait(false);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var result = await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "canceled" },
                new PipelinePublishOptions
                {
                    OverflowPolicyOverride = PipelineOverflowPolicy.Cancel,
                    CancellationToken = cancellation.Token
                })
            .ConfigureAwait(false);

        await DrainPipelineAsync(pipeline, destination).ConfigureAwait(false);

        Assert.False(result.Accepted);
        Assert.Equal(PipelinePublishResultReason.Canceled, result.Reason);
    }

    [Fact]
    public async Task Pipeline_Status_Events_And_PerformanceSnapshot_Report_FlowControl_State()
    {
        var (pipeline, destination) = CreatePipeline(
            "flow-status",
            new PipelineFlowControlOptions
            {
                OverflowPolicy = PipelineOverflowPolicy.Wait,
                SaturationWarningThreshold = 0.5d
            });

        var saturationEvents = new List<PipelineSaturationChangedEventArgs>();
        pipeline.OnSaturationChanged += (_, args) => saturationEvents.Add(args);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await SaturatePipelineAsync(pipeline, destination).ConfigureAwait(false);

        var rejectedResult = await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "rejected" },
                new PipelinePublishOptions
                {
                    OverflowPolicyOverride = PipelineOverflowPolicy.Reject
                })
            .ConfigureAwait(false);

        var saturatedStatus = pipeline.GetStatus();

        Assert.NotNull(saturatedStatus.FlowControlStatus);
        Assert.True(saturatedStatus.FlowControlStatus!.IsSaturated);
        Assert.True(saturatedStatus.FlowControlStatus.TotalBufferedCount >= 2);
        Assert.All(saturatedStatus.Components, component => Assert.NotNull(component.Flow));
        Assert.Contains(saturatedStatus.FlowControlStatus.Components, component => component.IsSaturated);
        Assert.False(rejectedResult.Accepted);

        await DrainPipelineAsync(pipeline, destination).ConfigureAwait(false);

        var completedStatus = pipeline.GetStatus();
        var snapshot = pipeline.GetPerformanceSnapshot();

        Assert.NotNull(completedStatus.FlowControlStatus);
        Assert.False(completedStatus.FlowControlStatus!.IsSaturated);
        Assert.Equal(0, completedStatus.FlowControlStatus.TotalBufferedCount);
        Assert.True(snapshot.PeakBufferedCount >= 2);
        Assert.Equal(1, snapshot.TotalPublishRejectedCount);
        Assert.Contains(saturationEvents, args => args.IsSaturated);
    }

    private static async Task SaturatePipelineAsync(
        IPipeline<TestPipelineRecord> pipeline,
        BlockingAsyncDestination destination)
    {
        await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "record-1" },
                new PipelinePublishOptions())
            .ConfigureAwait(false);
        await destination.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        var second = await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "record-2" },
                new PipelinePublishOptions())
            .ConfigureAwait(false);
        var third = await pipeline.PublishAsync(
                new TestPipelineRecord { Data = "record-3" },
                new PipelinePublishOptions())
            .ConfigureAwait(false);

        Assert.True(second.Accepted);
        Assert.True(third.Accepted);
    }

    private static async Task DrainPipelineAsync(
        IPipeline<TestPipelineRecord> pipeline,
        BlockingAsyncDestination destination)
    {
        destination.ReleaseExecution();
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);
    }

    private static (IPipeline<TestPipelineRecord> Pipeline, BlockingAsyncDestination Destination) CreatePipeline(
        string pipelineName,
        PipelineFlowControlOptions flowControlOptions)
    {
        var destination = new BlockingAsyncDestination();

        var pipeline = Pipeline<TestPipelineRecord>.New(pipelineName)
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                SourceExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 1,
                    DegreeOfParallelism = 1,
                    EnsureOrdered = true
                },
                DestinationExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 1,
                    DegreeOfParallelism = 1,
                    EnsureOrdered = true
                }
            })
            .UseFlowControlOptions(flowControlOptions)
            .WithInMemorySource(new object())
            .WithDestination(destination)
            .Build();

        return (pipeline, destination);
    }
}

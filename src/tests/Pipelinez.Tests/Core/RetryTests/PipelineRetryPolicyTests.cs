using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Retry;
using Pipelinez.Core.Status;
using Pipelinez.Tests.Core.RetryTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.RetryTests;

public class PipelineRetryPolicyTests
{
    [Fact]
    public async Task Pipeline_Segment_Retry_Recovers_And_Emits_Retry_Event()
    {
        var segment = new FlakySegment(failuresBeforeSuccess: 1);
        var retryEvents = new List<PipelineRecordRetryingEventArgs<TestPipelineRecord>>();
        var completedRecords = new List<TestPipelineRecord>();

        var pipeline = Pipeline<TestPipelineRecord>.New("segment-retry-recovers")
            .UseRetryOptions(new PipelineRetryOptions<TestPipelineRecord>
            {
                DefaultSegmentPolicy = PipelineRetryPolicy<TestPipelineRecord>
                    .FixedDelay(3, TimeSpan.Zero)
                    .Handle<RetryTestException>()
            })
            .WithInMemorySource(new object())
            .AddSegment(segment, new object())
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnPipelineRecordRetrying += (_, args) => retryEvents.Add(args);
        pipeline.OnPipelineRecordCompleted += (_, args) => completedRecords.Add(args.Record);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Single(retryEvents);
        Assert.Single(completedRecords);
        Assert.Equal("good|segment-ok", completedRecords[0].Data);
        Assert.Equal(2, segment.Attempts);
        Assert.Equal(1, retryEvents[0].AttemptNumber);
        Assert.Equal(nameof(FlakySegment), retryEvents[0].ComponentName);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.TotalRetryCount);
        Assert.Equal(1, snapshot.SuccessfulRetryRecoveries);
        Assert.Equal(0, snapshot.RetryExhaustions);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task Pipeline_Destination_Retry_Recovers_And_Completes()
    {
        var destination = new FlakyDestination(failuresBeforeSuccess: 2);

        var pipeline = Pipeline<TestPipelineRecord>.New("destination-retry-recovers")
            .UseRetryOptions(new PipelineRetryOptions<TestPipelineRecord>
            {
                DestinationPolicy = PipelineRetryPolicy<TestPipelineRecord>
                    .FixedDelay(4, TimeSpan.Zero)
                    .Handle<RetryTestException>()
            })
            .WithInMemorySource(new object())
            .AddSegment(new FlakySegment(failuresBeforeSuccess: 0), new object())
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Equal(3, destination.Attempts);
        Assert.Single(destination.ReceivedRecords);
        Assert.Equal("good|segment-ok", destination.ReceivedRecords[0]);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(2, snapshot.TotalRetryCount);
        Assert.Equal(1, snapshot.SuccessfulRetryRecoveries);
        Assert.Equal(0, snapshot.RetryExhaustions);
    }

    [Fact]
    public async Task Pipeline_Retry_Exhaustion_Flows_Into_Error_Handler_Context()
    {
        PipelineErrorContext<TestPipelineRecord>? capturedContext = null;
        var retryEvents = new List<PipelineRecordRetryingEventArgs<TestPipelineRecord>>();

        var pipeline = Pipeline<TestPipelineRecord>.New("retry-exhaustion")
            .UseRetryOptions(new PipelineRetryOptions<TestPipelineRecord>
            {
                DefaultSegmentPolicy = PipelineRetryPolicy<TestPipelineRecord>
                    .FixedDelay(2, TimeSpan.Zero)
                    .Handle<RetryTestException>()
            })
            .WithInMemorySource(new object())
            .AddSegment(new FlakySegment(failuresBeforeSuccess: 5), new object())
            .WithInMemoryDestination("config")
            .WithErrorHandler(context =>
            {
                capturedContext = context;
                return PipelineErrorAction.SkipRecord;
            })
            .Build();

        pipeline.OnPipelineRecordRetrying += (_, args) => retryEvents.Add(args);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "bad" }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Single(retryEvents);
        Assert.NotNull(capturedContext);
        Assert.True(capturedContext!.RetryExhausted);
        Assert.Equal(1, capturedContext.RetryAttemptCount);
        Assert.Single(capturedContext.RetryHistory);
        Assert.True(capturedContext.Container.HasFault);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.TotalRetryCount);
        Assert.Equal(0, snapshot.SuccessfulRetryRecoveries);
        Assert.Equal(1, snapshot.RetryExhaustions);
    }

    [Fact]
    public async Task Pipeline_Retry_Filter_Does_Not_Retry_NonMatching_Exceptions()
    {
        PipelineErrorContext<TestPipelineRecord>? capturedContext = null;
        var retryEvents = new List<PipelineRecordRetryingEventArgs<TestPipelineRecord>>();

        var pipeline = Pipeline<TestPipelineRecord>.New("retry-filter")
            .UseRetryOptions(new PipelineRetryOptions<TestPipelineRecord>
            {
                DefaultSegmentPolicy = PipelineRetryPolicy<TestPipelineRecord>
                    .FixedDelay(4, TimeSpan.Zero)
                    .Handle<RetryTestException>()
            })
            .WithInMemorySource(new object())
            .AddSegment(new FlakySegment(failuresBeforeSuccess: 1, throwNonRetryable: true), new object())
            .WithInMemoryDestination("config")
            .WithErrorHandler(context =>
            {
                capturedContext = context;
                return PipelineErrorAction.SkipRecord;
            })
            .Build();

        pipeline.OnPipelineRecordRetrying += (_, args) => retryEvents.Add(args);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "bad" }).ConfigureAwait(false);
        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Empty(retryEvents);
        Assert.NotNull(capturedContext);
        Assert.False(capturedContext!.RetryExhausted);
        Assert.Equal(0, capturedContext.RetryAttemptCount);
    }
}

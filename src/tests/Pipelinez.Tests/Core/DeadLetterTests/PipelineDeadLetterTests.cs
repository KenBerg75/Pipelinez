using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Retry;
using Pipelinez.Core.Status;
using Pipelinez.Tests.Core.DeadLetterTests.Models;
using Pipelinez.Tests.Core.ErrorHandlingTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DeadLetterTests;

public sealed class PipelineDeadLetterTests
{
    [Fact]
    public async Task Pipeline_DeadLetter_Writes_Faulted_Record_And_Allows_Later_Records_To_Continue()
    {
        var deadLetters = new InMemoryDeadLetterDestination<TestPipelineRecord>();
        var destination = new CollectingDestination();
        var faultEvents = new List<PipelineRecordFaultedEventArgs<TestPipelineRecord>>();
        var deadLetterEvents = new List<PipelineRecordDeadLetteredEventArgs<TestPipelineRecord>>();

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_DeadLetter_Writes_Faulted_Record_And_Allows_Later_Records_To_Continue))
            .UseRetryOptions(new PipelineRetryOptions<TestPipelineRecord>
            {
                DefaultSegmentPolicy = PipelineRetryPolicy<TestPipelineRecord>
                    .FixedDelay(2, TimeSpan.Zero)
                    .Handle<InvalidOperationException>()
            })
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithDestination(destination)
            .WithDeadLetterDestination(deadLetters)
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        pipeline.OnPipelineRecordFaulted += (_, args) => faultEvents.Add(args);
        pipeline.OnPipelineRecordDeadLettered += (_, args) => deadLetterEvents.Add(args);

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" });
        await pipeline.CompleteAsync();

        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
        Assert.Single(faultEvents);
        Assert.Single(deadLetterEvents);
        Assert.Single(destination.Records);
        Assert.Single(deadLetters.Records);

        var goodRecord = Assert.Single(destination.Records);
        Assert.Equal("good|processed", goodRecord.Data);

        var deadLetter = Assert.Single(deadLetters.Records);
        Assert.Equal(ConditionalFaultingSegment.FaultingValue, deadLetter.Record.Data);
        Assert.Equal(nameof(ConditionalFaultingSegment), deadLetter.Fault.ComponentName);
        Assert.Equal(PipelineComponentKind.Segment, deadLetter.Fault.ComponentKind);
        Assert.Single(deadLetter.RetryHistory);
        Assert.Single(deadLetter.SegmentHistory);
        Assert.False(deadLetter.SegmentHistory[0].Succeeded);
        Assert.True(deadLetter.DeadLetteredAtUtc >= deadLetter.CreatedAtUtc);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.TotalRecordsFaulted);
        Assert.Equal(1, snapshot.TotalDeadLetteredCount);
        Assert.Equal(0, snapshot.TotalDeadLetterFailureCount);
    }

    [Fact]
    public async Task Pipeline_DeadLetter_Without_Configured_Destination_Faults_The_Runtime()
    {
        PipelineFaultedEventArgs? pipelineFault = null;

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_DeadLetter_Without_Configured_Destination_Faults_The_Runtime))
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithInMemoryDestination("config")
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        pipeline.OnPipelineFaulted += (_, args) => pipelineFault = args;

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Contains("no dead-letter destination is configured", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(pipelineFault);
        Assert.Equal("PipelineDeadLetterDestination", pipelineFault!.ComponentName);
        Assert.Equal(PipelineComponentKind.Pipeline, pipelineFault.ComponentKind);
        Assert.Equal(1, pipeline.GetPerformanceSnapshot().TotalDeadLetterFailureCount);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task Pipeline_DeadLetter_Write_Failure_Faults_The_Runtime_By_Default()
    {
        var deadLetterException = new InvalidOperationException("Dead-letter write failed.");
        var deadLetterFailures = new List<PipelineDeadLetterWriteFailedEventArgs<TestPipelineRecord>>();
        PipelineFaultedEventArgs? pipelineFault = null;

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_DeadLetter_Write_Failure_Faults_The_Runtime_By_Default))
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithInMemoryDestination("config")
            .WithDeadLetterDestination(new ThrowingDeadLetterDestination<TestPipelineRecord>(deadLetterException))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        pipeline.OnPipelineDeadLetterWriteFailed += (_, args) => deadLetterFailures.Add(args);
        pipeline.OnPipelineFaulted += (_, args) => pipelineFault = args;

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Equal(deadLetterException.Message, exception.Message);
        Assert.Single(deadLetterFailures);
        Assert.NotNull(pipelineFault);
        Assert.StartsWith("ThrowingDeadLetterDestination", pipelineFault!.ComponentName, StringComparison.Ordinal);
        Assert.Equal(PipelineComponentKind.Destination, pipelineFault.ComponentKind);
        Assert.Equal(1, pipeline.GetPerformanceSnapshot().TotalDeadLetterFailureCount);
    }

    [Fact]
    public async Task Pipeline_DeadLetter_Write_Failure_Can_Be_Configured_To_Continue()
    {
        var destination = new CollectingDestination();
        var deadLetterFailures = new List<PipelineDeadLetterWriteFailedEventArgs<TestPipelineRecord>>();

        var pipeline = Pipeline<TestPipelineRecord>.New(nameof(Pipeline_DeadLetter_Write_Failure_Can_Be_Configured_To_Continue))
            .UseDeadLetterOptions(new PipelineDeadLetterOptions
            {
                TreatDeadLetterFailureAsPipelineFault = false
            })
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithDestination(destination)
            .WithDeadLetterDestination(
                new ThrowingDeadLetterDestination<TestPipelineRecord>(
                    new InvalidOperationException("Dead-letter failure is tolerated.")))
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        pipeline.OnPipelineDeadLetterWriteFailed += (_, args) => deadLetterFailures.Add(args);

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" });
        await pipeline.CompleteAsync();

        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
        Assert.Single(deadLetterFailures);
        var goodRecord = Assert.Single(destination.Records);
        Assert.Equal("good|processed", goodRecord.Data);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.TotalDeadLetterFailureCount);
        Assert.Equal(0, snapshot.TotalDeadLetteredCount);
    }
}

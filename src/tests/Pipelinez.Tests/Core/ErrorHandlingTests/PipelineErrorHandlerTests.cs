using Pipelinez.Core;
using Pipelinez.Core.Destination;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;
using Pipelinez.Core.Status;
using Pipelinez.Tests.Core.ErrorHandlingTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.ErrorHandlingTests;

public class PipelineErrorHandlerTests
{
    [Fact]
    public async Task Pipeline_ErrorHandler_Is_Called_When_A_Segment_Throws_And_StopPipeline_Faults_The_Runtime()
    {
        PipelineErrorContext<TestPipelineRecord>? capturedContext = null;
        var eventOrder = new List<string>();
        PipelineFaultedEventArgs? pipelineFaultArgs = null;

        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_ErrorHandler_Is_Called_When_A_Segment_Throws_And_StopPipeline_Faults_The_Runtime")
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithInMemoryDestination("config")
            .WithErrorHandler(context =>
            {
                capturedContext = context;
                return PipelineErrorAction.StopPipeline;
            })
            .Build();

        pipeline.OnPipelineRecordFaulted += (_, _) => eventOrder.Add("record");
        pipeline.OnPipelineFaulted += (_, args) =>
        {
            eventOrder.Add("pipeline");
            pipelineFaultArgs = args;
        };

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Equal(ConditionalFaultingSegment.FailureMessage, exception.Message);
        Assert.Equal(new[] { "record", "pipeline" }, eventOrder);
        Assert.NotNull(capturedContext);
        Assert.Equal(nameof(ConditionalFaultingSegment), capturedContext!.ComponentName);
        Assert.Equal(PipelineComponentKind.Segment, capturedContext.ComponentKind);
        Assert.Equal(ConditionalFaultingSegment.FailureMessage, capturedContext.Exception.Message);
        Assert.True(capturedContext.Container.HasFault);
        Assert.NotNull(capturedContext.Container.Fault);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);

        Assert.NotNull(pipelineFaultArgs);
        Assert.Equal(nameof(ConditionalFaultingSegment), pipelineFaultArgs!.ComponentName);
        Assert.Equal(PipelineComponentKind.Segment, pipelineFaultArgs.ComponentKind);
    }

    [Fact]
    public async Task Pipeline_ErrorHandler_Is_Called_When_A_Destination_Throws()
    {
        PipelineErrorContext<TestPipelineRecord>? capturedContext = null;

        var pipeline = CreatePipeline(
            "Pipeline_ErrorHandler_Is_Called_When_A_Destination_Throws",
            new ConditionalFaultingDestination(),
            context =>
            {
                capturedContext = context;
                return PipelineErrorAction.StopPipeline;
            });

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingDestination.FaultingValue });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Equal(ConditionalFaultingDestination.FailureMessage, exception.Message);
        Assert.NotNull(capturedContext);
        Assert.Equal(nameof(ConditionalFaultingDestination), capturedContext!.ComponentName);
        Assert.Equal(PipelineComponentKind.Destination, capturedContext.ComponentKind);
        Assert.Equal(ConditionalFaultingDestination.FailureMessage, capturedContext.Exception.Message);
        Assert.True(capturedContext.Container.HasFault);
        Assert.NotNull(capturedContext.Container.Fault);
    }

    [Fact]
    public async Task Pipeline_ErrorHandler_SkipRecord_Allows_Later_Records_To_Continue()
    {
        var completedRecords = new List<TestPipelineRecord>();
        var faultedRecords = new List<PipelineRecordFaultedEventArgs<TestPipelineRecord>>();
        var pipelineFaultedRaised = false;

        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_ErrorHandler_SkipRecord_Allows_Later_Records_To_Continue")
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithInMemoryDestination("config")
            .WithErrorHandler(context =>
            {
                if (context.Record.Data == ConditionalFaultingSegment.FaultingValue)
                {
                    return PipelineErrorAction.SkipRecord;
                }

                return PipelineErrorAction.StopPipeline;
            })
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) => completedRecords.Add(args.Record);
        pipeline.OnPipelineRecordFaulted += (_, args) => faultedRecords.Add(args);
        pipeline.OnPipelineFaulted += (_, _) => pipelineFaultedRaised = true;

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "good" });
        await pipeline.CompleteAsync();

        Assert.False(pipelineFaultedRaised);
        Assert.Single(faultedRecords);
        Assert.Single(completedRecords);
        Assert.Equal("good|processed", completedRecords[0].Data);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task Pipeline_ErrorHandler_Exceptions_Fault_The_Pipeline()
    {
        PipelineFaultedEventArgs? pipelineFaultArgs = null;
        PipelineErrorHandler<TestPipelineRecord> errorHandler =
            _ => throw new InvalidOperationException("Handler failed intentionally.");

        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_ErrorHandler_Exceptions_Fault_The_Pipeline")
            .WithInMemorySource("config")
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithInMemoryDestination("config")
            .WithErrorHandler(errorHandler)
            .Build();

        pipeline.OnPipelineFaulted += (_, args) => pipelineFaultArgs = args;

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Equal("Handler failed intentionally.", exception.Message);
        Assert.NotNull(pipelineFaultArgs);
        Assert.Equal("PipelineErrorHandler", pipelineFaultArgs!.ComponentName);
        Assert.Equal(PipelineComponentKind.Pipeline, pipelineFaultArgs.ComponentKind);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }

    private static Pipeline<TestPipelineRecord> CreatePipeline(
        string name,
        IPipelineDestination<TestPipelineRecord> destination,
        Func<PipelineErrorContext<TestPipelineRecord>, PipelineErrorAction> errorHandler)
    {
        var pipeline = new Pipeline<TestPipelineRecord>(
            name,
            new InMemoryPipelineSource<TestPipelineRecord>(),
            destination,
            new List<IPipelineSegment<TestPipelineRecord>>(),
            context => Task.FromResult(errorHandler(context)));

        pipeline.LinkPipeline();
        pipeline.InitializePipeline();

        return pipeline;
    }
}

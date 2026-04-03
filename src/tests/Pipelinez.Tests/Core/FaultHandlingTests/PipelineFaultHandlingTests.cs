using Pipelinez.Core;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;
using Pipelinez.Core.Status;
using Pipelinez.Tests.Core.FaultHandlingTests.Models;
using Pipelinez.Tests.Core.SegmentTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.FaultHandlingTests;

public class PipelineFaultHandlingTests
{
    [Fact]
    public async Task Pipeline_Segment_Fault_Populates_Fault_State_History_And_Events()
    {
        var testRecord = new TestSegmentModel
        {
            FirstValue = 5,
            SecondValue = 7
        };

        var pipeline = Pipeline<TestSegmentModel>.New("Pipeline_Segment_Fault_Populates_Fault_State_History_And_Events")
            .WithInMemorySource("config")
            .AddSegment(new TestAddSegment(), "config")
            .AddSegment(new FaultingSegment(), "config")
            .AddSegment(new TestMultiplySegment(), "config")
            .WithInMemoryDestination("config")
            .Build();

        var eventOrder = new List<string>();
        PipelineRecordFaultedEventArgs<TestSegmentModel>? recordFaultArgs = null;
        PipelineFaultedEventArgs? pipelineFaultArgs = null;
        var completedRaised = false;

        pipeline.OnPipelineRecordCompleted += (_, _) => completedRaised = true;
        pipeline.OnPipelineRecordFaulted += (_, args) =>
        {
            eventOrder.Add("record");
            recordFaultArgs = args;
        };
        pipeline.OnPipelineFaulted += (_, args) =>
        {
            eventOrder.Add("pipeline");
            pipelineFaultArgs = args;
        };

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(testRecord);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Equal(FaultingSegment.FailureMessage, exception.Message);
        Assert.False(completedRaised);
        Assert.Equal(new[] { "record", "pipeline" }, eventOrder);

        Assert.NotNull(recordFaultArgs);
        Assert.NotNull(recordFaultArgs!.Container);
        Assert.True(recordFaultArgs.Container.HasFault);
        Assert.NotNull(recordFaultArgs.Container.Fault);
        Assert.Equal(PipelineComponentKind.Segment, recordFaultArgs.Fault.ComponentKind);
        Assert.Equal(nameof(FaultingSegment), recordFaultArgs.Fault.ComponentName);
        Assert.Equal(FaultingSegment.FailureMessage, recordFaultArgs.Fault.Message);
        Assert.Same(testRecord, recordFaultArgs.Record);

        Assert.Equal(2, recordFaultArgs.Container.SegmentHistory.Count);
        Assert.Equal(nameof(TestAddSegment), recordFaultArgs.Container.SegmentHistory[0].SegmentName);
        Assert.True(recordFaultArgs.Container.SegmentHistory[0].Succeeded);
        Assert.Equal(nameof(FaultingSegment), recordFaultArgs.Container.SegmentHistory[1].SegmentName);
        Assert.False(recordFaultArgs.Container.SegmentHistory[1].Succeeded);
        Assert.Equal(FaultingSegment.FailureMessage, recordFaultArgs.Container.SegmentHistory[1].FailureMessage);

        Assert.Equal(testRecord.FirstValue + testRecord.SecondValue, testRecord.AddResult);
        Assert.Equal(0, testRecord.MultiplyResult);

        Assert.NotNull(pipelineFaultArgs);
        Assert.Equal(PipelineComponentKind.Segment, pipelineFaultArgs!.ComponentKind);
        Assert.Equal(nameof(FaultingSegment), pipelineFaultArgs.ComponentName);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task Pipeline_Destination_Fault_Populates_Fault_State_And_Raises_Fault_Events()
    {
        var pipeline = CreatePipeline(
            "Pipeline_Destination_Fault_Populates_Fault_State_And_Raises_Fault_Events",
            new FaultingDestination());
        var testRecord = new TestPipelineRecord { Data = "Test" };

        PipelineRecordFaultedEventArgs<TestPipelineRecord>? recordFaultArgs = null;
        PipelineFaultedEventArgs? pipelineFaultArgs = null;
        var completedRaised = false;

        pipeline.OnPipelineRecordCompleted += (_, _) => completedRaised = true;
        pipeline.OnPipelineRecordFaulted += (_, args) => recordFaultArgs = args;
        pipeline.OnPipelineFaulted += (_, args) => pipelineFaultArgs = args;

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(testRecord);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion);

        Assert.Equal(FaultingDestination.FailureMessage, exception.Message);
        Assert.False(completedRaised);

        Assert.NotNull(recordFaultArgs);
        Assert.True(recordFaultArgs!.Container.HasFault);
        Assert.NotNull(recordFaultArgs.Container.Fault);
        Assert.Equal(PipelineComponentKind.Destination, recordFaultArgs.Fault.ComponentKind);
        Assert.Equal(nameof(FaultingDestination), recordFaultArgs.Fault.ComponentName);
        Assert.Empty(recordFaultArgs.Container.SegmentHistory);

        Assert.NotNull(pipelineFaultArgs);
        Assert.Equal(PipelineComponentKind.Destination, pipelineFaultArgs!.ComponentKind);
        Assert.Equal(nameof(FaultingDestination), pipelineFaultArgs.ComponentName);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }

    private static Pipeline<TestPipelineRecord> CreatePipeline(
        string name,
        IPipelineDestination<TestPipelineRecord> destination)
    {
        var pipeline = new Pipeline<TestPipelineRecord>(
            name,
            new InMemoryPipelineSource<TestPipelineRecord>(),
            destination,
            new List<IPipelineSegment<TestPipelineRecord>>());

        pipeline.LinkPipeline();
        pipeline.InitializePipeline();

        return pipeline;
    }
}

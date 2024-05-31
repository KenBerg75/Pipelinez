using Pipelinez.Core;
using Pipelinez.Tests.Core.SegmentTests.Models;

namespace Pipelinez.Tests.Core.SegmentTests;

public class PipelineSegmentTests
{
    [Fact]
    public async void Pipeline_Segment_Operates_As_Expected()
    {
        var testRecord = new TestSegmentModel()
        {
            FirstValue = 93,
            SecondValue = 105
        };
        
        var pipeline = Pipeline<TestSegmentModel>.New("Pipeline_Segment_Operates_As_Expected")
            .WithInMemorySource("config")
            .AddSegment(new TestAddSegment(), "config")
            .WithInMemoryDestination("config")
            .Build();
        
        pipeline.OnPipelineRecordCompleted += (sender, args) =>
        {
            Assert.Equal(testRecord.FirstValue + testRecord.SecondValue, args.Record.AddResult);
        };

        pipeline.StartPipelineAsync(new CancellationTokenSource());
        
        await pipeline.PublishAsync(testRecord);
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();
    }
    
    
    [Fact]
    public async void Pipeline_Multiple_Segment_Operates_As_Expected()
    {
        var testRecord = new TestSegmentModel()
        {
            FirstValue = 93,
            SecondValue = 105
        };
        
        var pipeline = Pipeline<TestSegmentModel>.New("Pipeline_Multiple_Segment_Operates_As_Expected")
            .WithInMemorySource("config")
            .AddSegment(new TestAddSegment(), "config")
            .AddSegment(new TestMultiplySegment(), "config")
            .WithInMemoryDestination("config")
            .Build();
        
        pipeline.OnPipelineRecordCompleted += (sender, args) =>
        {
            Assert.Equal(testRecord.FirstValue + testRecord.SecondValue, args.Record.AddResult);
            Assert.Equal(testRecord.FirstValue * testRecord.SecondValue, args.Record.MultiplyResult);
        };

        pipeline.StartPipelineAsync(new CancellationTokenSource());
        
        await pipeline.PublishAsync(testRecord);
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();
    }
    
    [Fact]
    public async void Pipeline_Multiple_Segment_Ordering_Operates_As_Expected()
    {
        var testRecord = new TestOrderModel();
        
        var pipeline = Pipeline<TestOrderModel>.New("Pipeline_Multiple_Segment_Ordering_Operates_As_Expected")
            .WithInMemorySource("config")
            .AddSegment(new TestOrderFirstSegment(), "config")
            .AddSegment(new TestOrderSecondSegment(), "config")
            .WithInMemoryDestination("config")
            .Build();
        
        pipeline.OnPipelineRecordCompleted += (sender, args) =>
        {
            Assert.True(args.Record.FirstStamp < args.Record.SecondStamp);
        };

        pipeline.StartPipelineAsync(new CancellationTokenSource());
        
        await pipeline.PublishAsync(testRecord);
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();
    }
}
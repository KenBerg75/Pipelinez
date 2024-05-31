using Pipelinez.Core;
using Pipelinez.Tests.Core.LoggingTests.Models;

namespace Pipelinez.Tests.Core.LoggingTests;

public class PipelineLoggingTests
{
    [Fact]
    public async void Pipeline_Segment_Logging_Operates_As_Expected()
    {
        var testRecord = new TestLoggingModel();
        var testLogFactory = new TestLogFactory();
        
        var pipeline = Pipeline<TestLoggingModel>.New("Pipeline_Segment_Logging_Operates_As_Expected")
            .UseLogger(testLogFactory)
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();

        pipeline.StartPipelineAsync(new CancellationTokenSource());
        
        await pipeline.PublishAsync(testRecord);
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();
        
        // Assert that the logger was called
        Assert.NotEqual(0, testLogFactory.LoggersCreated);
    }
    
    [Fact]
    public async void Pipeline_Segment_Logging_Throws_With_Null()
    {
        var testRecord = new TestLoggingModel();
        
        Assert.Throws<ArgumentNullException>(() => 
            Pipeline<TestLoggingModel>.New("Pipeline_Segment_Logging_Throws_With_Null")
            .UseLogger(null)
            .WithInMemorySource("config")
            .WithInMemoryDestination("config").Build());
    }
}
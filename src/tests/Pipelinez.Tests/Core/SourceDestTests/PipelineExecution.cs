using Pipelinez.Core;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.SourceDestTests;

public class PipelineExecution
{
    [Fact]
    public async void Pipeline_Executes_With_Source_Dest()
    {
        var token = new CancellationTokenSource();
        var testRecord = new TestPipelineRecord(){Data = "Test"};
        
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Builds_With_Source_Dest")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();
        
        // register to receive the record
        pipeline.OnPipelineRecordCompleted += (sender, args) =>
        {
            Assert.NotNull(args.Record);
            Assert.Equal(testRecord.Data, args.Record.Data);
        };

        await pipeline.StartAsync(token);
        
        await pipeline.PublishAsync(testRecord);
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();

        Assert.NotNull(pipeline);
    }
    
    [Fact]
    public async void Pipeline_Executes_Multiple_With_Source_Dest()
    {
        var token = new CancellationTokenSource();
        var rand = new Random().Next(100,1000);
        var testRecords = new List<TestPipelineRecord>();

        for (int i = 0; i < rand; i++)
        {
            testRecords.Add(new TestPipelineRecord(){Data = $"{i}"});
        }
        
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Executes_Multiple_With_Source_Dest")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();
        
        var receivedRecords = 0;
        // register to receive the record
        pipeline.OnPipelineRecordCompleted += (sender, args) =>
        {
            receivedRecords++;
            Assert.NotNull(args.Record);
        };

        await pipeline.StartAsync(token);
        foreach(var record in testRecords)
        {
            await pipeline.PublishAsync(record);
        }
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();

        Assert.Equal(testRecords.Count, receivedRecords);
    }
}
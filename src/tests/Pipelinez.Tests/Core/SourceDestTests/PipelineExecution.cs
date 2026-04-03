using Pipelinez.Core;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.SourceDestTests;

public class PipelineExecution
{
    [Fact]
    public async Task Pipeline_Executes_With_Source_Dest()
    {
        var testRecord = new TestPipelineRecord(){Data = "Test"};
        
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Builds_With_Source_Dest")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            //.WithErrorHandler(((exception, container) =>
            //{
                // Do something useful with the error
            //})
            //.WithKafkaErrorHandler("kafkaConfig")
            //.WithLoggingErrorHandler() // shouldn't need config - uses logger
            .Build();
        
        // register to receive the record
        pipeline.OnPipelineRecordCompleted += (sender, args) =>
        {
            Assert.NotNull(args.Record);
            Assert.Equal(testRecord.Data, args.Record.Data);
        };

        await pipeline.StartPipelineAsync();
        
        await pipeline.PublishAsync(testRecord);
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();

        Assert.NotNull(pipeline);
    }
    
    [Fact]
    public async Task Pipeline_Executes_Multiple_With_Source_Dest()
    {
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

        await pipeline.StartPipelineAsync();
        foreach(var record in testRecords)
        {
            await pipeline.PublishAsync(record);
        }
        
        // Wait for the pipeline to complete
        await pipeline.CompleteAsync();

        Assert.Equal(testRecords.Count, receivedRecords);
    }

    [Fact]
    public async Task Pipeline_Publish_Throws_Before_Start()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Publish_Throws_Before_Start")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.PublishAsync(new TestPipelineRecord { Data = "Test" }));

        Assert.Contains("must be running", exception.Message);
    }

    [Fact]
    public async Task Pipeline_Complete_Throws_Before_Start()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Complete_Throws_Before_Start")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.CompleteAsync());

        Assert.Contains("must be started", exception.Message);
    }

    [Fact]
    public async Task Pipeline_Start_Throws_On_Double_Start()
    {
        var pipeline = Pipeline<TestPipelineRecord>.New("Pipeline_Start_Throws_On_Double_Start")
            .WithInMemorySource("config")
            .WithInMemoryDestination("config")
            .Build();

        await pipeline.StartPipelineAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.StartPipelineAsync());

        Assert.Contains("cannot be started", exception.Message);

        await pipeline.CompleteAsync();
    }
}

using Pipelinez.Core;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;
using Pipelinez.Tests.Core.DestinationTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DestinationTests;

public class AsyncDestinationExecutionTests
{
    [Fact]
    public async Task Pipeline_Completion_Waits_For_Async_Destination_Work()
    {
        var destination = new BlockingAsyncDestination();
        var pipeline = CreatePipeline(
            "Pipeline_Completion_Waits_For_Async_Destination_Work",
            destination);

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "test" });

        var completeTask = pipeline.CompleteAsync();

        await destination.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(completeTask.IsCompleted);

        destination.ReleaseExecution();

        await completeTask;
        await pipeline.Completion;

        Assert.True(destination.ExecutionCompleted.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Async_Destination_Exception_Faults_The_Pipeline()
    {
        var destination = new AsyncFaultingDestination();
        var pipeline = CreatePipeline(
            "Async_Destination_Exception_Faults_The_Pipeline",
            destination);

        await pipeline.StartPipelineAsync();
        await pipeline.PublishAsync(new TestPipelineRecord { Data = "test" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.CompleteAsync());

        Assert.Equal(AsyncFaultingDestination.FailureMessage, exception.Message);
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

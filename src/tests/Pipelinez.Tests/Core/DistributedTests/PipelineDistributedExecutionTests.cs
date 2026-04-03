using System.Collections.Concurrent;
using Pipelinez.Core;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Status;
using Pipelinez.Tests.Core.DistributedTests.Models;
using Pipelinez.Tests.Core.ErrorHandlingTests.Models;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.DistributedTests;

public class PipelineDistributedExecutionTests
{
    [Fact]
    public void Pipeline_Build_Fails_When_Distributed_Mode_Is_Used_With_A_NonDistributed_Source()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Pipeline<TestPipelineRecord>.New("distributed-invalid-source")
                .UseHostOptions(new PipelineHostOptions
                {
                    ExecutionMode = PipelineExecutionMode.Distributed
                })
                .WithInMemorySource("config")
                .WithInMemoryDestination("config")
                .Build());

        Assert.Contains("does not support distributed ownership", exception.Message);
    }

    [Fact]
    public async Task Pipeline_Distributed_Runtime_Context_Status_And_Events_Are_Populated()
    {
        var source = new ManualDistributedSource();
        var workerStarted = new ConcurrentQueue<PipelineRuntimeContext>();
        var assignedEvents = new ConcurrentQueue<IReadOnlyList<PipelinePartitionLease>>();
        var revokedEvents = new ConcurrentQueue<IReadOnlyList<PipelinePartitionLease>>();
        var workerStopping = new ConcurrentQueue<PipelineRuntimeContext>();

        var pipeline = Pipeline<TestPipelineRecord>.New("distributed-runtime")
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "instance-a",
                WorkerId = "worker-a"
            })
            .WithSource(source)
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnWorkerStarted += (_, args) => workerStarted.Enqueue(args.RuntimeContext);
        pipeline.OnPartitionsAssigned += (_, args) => assignedEvents.Enqueue(args.Partitions);
        pipeline.OnPartitionsRevoked += (_, args) => revokedEvents.Enqueue(args.Partitions);
        pipeline.OnWorkerStopping += (_, args) => workerStopping.Enqueue(args.RuntimeContext);

        await pipeline.StartPipelineAsync();

        var runtimeContext = pipeline.GetRuntimeContext();
        var partition0 = new PipelinePartitionLease("lease-0", "TestTransport", "partition-0", runtimeContext.InstanceId, runtimeContext.WorkerId, 0);
        var partition1 = new PipelinePartitionLease("lease-1", "TestTransport", "partition-1", runtimeContext.InstanceId, runtimeContext.WorkerId, 1);

        source.AssignPartitions(partition0, partition1);
        source.RevokePartitions(partition1);

        var activeContext = pipeline.GetRuntimeContext();
        Assert.Single(activeContext.OwnedPartitions);
        Assert.Equal("lease-0", activeContext.OwnedPartitions[0].LeaseId);

        var activeDistributedStatus = pipeline.GetStatus().DistributedStatus;
        Assert.NotNull(activeDistributedStatus);
        Assert.Equal(PipelineExecutionMode.Distributed, activeDistributedStatus!.ExecutionMode);
        Assert.Equal("worker-a", activeDistributedStatus.WorkerId);
        Assert.Single(activeDistributedStatus.OwnedPartitions);
        Assert.Equal("lease-0", activeDistributedStatus.OwnedPartitions[0].LeaseId);

        await pipeline.CompleteAsync();
        await pipeline.Completion;

        Assert.Single(workerStarted);
        Assert.Single(workerStopping);
        Assert.Single(assignedEvents);
        Assert.Single(revokedEvents);

        var startedContext = Assert.Single(workerStarted);
        Assert.Equal(PipelineExecutionMode.Distributed, startedContext.ExecutionMode);
        Assert.Equal("instance-a", startedContext.InstanceId);
        Assert.Equal("worker-a", startedContext.WorkerId);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task Pipeline_Distributed_Record_Completed_Event_Includes_Distribution_Context()
    {
        var source = new ManualDistributedSource();
        PipelineRecordCompletedEventHandlerArgs<TestPipelineRecord>? completedArgs = null;

        var pipeline = Pipeline<TestPipelineRecord>.New("distributed-record-completed")
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "instance-b",
                WorkerId = "worker-b"
            })
            .WithSource(source)
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) => completedArgs = args;

        await pipeline.StartPipelineAsync();

        var context = pipeline.GetRuntimeContext();
        var lease = new PipelinePartitionLease("lease-10", "TestTransport", "partition-10", context.InstanceId, context.WorkerId, 10);
        source.AssignPartitions(lease);

        await source.PublishDistributedAsync(new TestPipelineRecord { Data = "good" }, lease, offset: 42);
        await pipeline.CompleteAsync();
        await pipeline.Completion;

        Assert.NotNull(completedArgs);
        Assert.NotNull(completedArgs!.Distribution);
        Assert.Equal("worker-b", completedArgs.Distribution!.WorkerId);
        Assert.Equal("instance-b", completedArgs.Distribution.InstanceId);
        Assert.Equal("TestTransport", completedArgs.Distribution.TransportName);
        Assert.Equal("lease-10", completedArgs.Distribution.LeaseId);
        Assert.Equal("partition-10", completedArgs.Distribution.PartitionKey);
        Assert.Equal(10, completedArgs.Distribution.PartitionId);
        Assert.Equal(42, completedArgs.Distribution.Offset);
    }

    [Fact]
    public async Task Pipeline_Distributed_Record_Faulted_Event_Includes_Distribution_Context()
    {
        var source = new ManualDistributedSource();
        PipelineRecordFaultedEventArgs<TestPipelineRecord>? faultedArgs = null;

        var pipeline = Pipeline<TestPipelineRecord>.New("distributed-record-faulted")
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "instance-c",
                WorkerId = "worker-c"
            })
            .WithSource(source)
            .AddSegment(new ConditionalFaultingSegment(), "config")
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnPipelineRecordFaulted += (_, args) => faultedArgs = args;

        await pipeline.StartPipelineAsync();

        var context = pipeline.GetRuntimeContext();
        var lease = new PipelinePartitionLease("lease-20", "TestTransport", "partition-20", context.InstanceId, context.WorkerId, 20);
        source.AssignPartitions(lease);

        await source.PublishDistributedAsync(
            new TestPipelineRecord { Data = ConditionalFaultingSegment.FaultingValue },
            lease,
            offset: 84);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.Completion);

        Assert.NotNull(faultedArgs);
        Assert.NotNull(faultedArgs!.Distribution);
        Assert.Equal("worker-c", faultedArgs.Distribution!.WorkerId);
        Assert.Equal("instance-c", faultedArgs.Distribution.InstanceId);
        Assert.Equal("TestTransport", faultedArgs.Distribution.TransportName);
        Assert.Equal("lease-20", faultedArgs.Distribution.LeaseId);
        Assert.Equal("partition-20", faultedArgs.Distribution.PartitionKey);
        Assert.Equal(20, faultedArgs.Distribution.PartitionId);
        Assert.Equal(84, faultedArgs.Distribution.Offset);
    }
}

using System.Collections.Concurrent;
using Pipelinez.Core;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Status;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPipelineDistributedTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_Distributed_Mode_Populates_Status_Events_And_Record_Distribution_Context()
    {
        var scenarioName = nameof(KafkaPipeline_Distributed_Mode_Populates_Status_Events_And_Record_Distribution_Context);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source", partitions: 1).ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        PipelineRuntimeContext? workerStarted = null;
        PipelineRuntimeContext? workerStopping = null;
        var assignedPartitions = new ConcurrentQueue<IReadOnlyList<PipelinePartitionLease>>();
        PipelineRecordCompletedEventHandlerArgs<TestKafkaRecord>? completedArgs = null;

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "kafka-instance-a",
                WorkerId = "kafka-worker-a"
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnWorkerStarted += (_, args) => workerStarted = args.RuntimeContext;
        pipeline.OnWorkerStopping += (_, args) => workerStopping = args.RuntimeContext;
        pipeline.OnPartitionsAssigned += (_, args) => assignedPartitions.Enqueue(args.Partitions);
        pipeline.OnPipelineRecordCompleted += (_, args) => completedArgs = args;

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => pipeline.GetRuntimeContext().OwnedPartitions.Count == 1,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("alpha", "value-1")
            }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => completedArgs is not null,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        var activeDistributedStatus = pipeline.GetStatus().DistributedStatus;
        Assert.NotNull(activeDistributedStatus);
        Assert.Equal(PipelineExecutionMode.Distributed, activeDistributedStatus!.ExecutionMode);
        Assert.Equal("kafka-worker-a", activeDistributedStatus.WorkerId);
        Assert.Single(activeDistributedStatus.OwnedPartitions);
        Assert.Single(activeDistributedStatus.PartitionExecution);
        Assert.True(activeDistributedStatus.PartitionExecution[0].IsAssigned);
        Assert.False(activeDistributedStatus.PartitionExecution[0].IsDraining);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.NotNull(workerStarted);
        Assert.NotNull(workerStopping);
        Assert.NotEmpty(assignedPartitions);
        Assert.NotNull(completedArgs);
        Assert.NotNull(completedArgs!.Distribution);

        Assert.Equal(PipelineExecutionMode.Distributed, workerStarted!.ExecutionMode);
        Assert.Equal("kafka-instance-a", workerStarted.InstanceId);
        Assert.Equal("kafka-worker-a", workerStarted.WorkerId);

        Assert.Equal("kafka-worker-a", completedArgs.Distribution!.WorkerId);
        Assert.Equal("kafka-instance-a", completedArgs.Distribution.InstanceId);
        Assert.Equal("Kafka", completedArgs.Distribution.TransportName);
        Assert.Equal(0, completedArgs.Distribution.PartitionId);
        Assert.Equal(0, completedArgs.Distribution.Offset);

        var distributedStatus = pipeline.GetStatus().DistributedStatus;
        Assert.NotNull(distributedStatus);
        Assert.Equal(PipelineExecutionMode.Distributed, distributedStatus!.ExecutionMode);
        Assert.Equal("kafka-worker-a", distributedStatus.WorkerId);
        Assert.Empty(distributedStatus.OwnedPartitions);
        Assert.Empty(distributedStatus.PartitionExecution);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task KafkaPipeline_Distributed_Mode_Rebalances_Across_Workers_And_Reassigns_On_Shutdown()
    {
        var scenarioName = nameof(KafkaPipeline_Distributed_Mode_Rebalances_Across_Workers_And_Reassigns_On_Shutdown);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source", partitions: 2).ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        var worker1Completions = new ConcurrentQueue<PipelineRecordCompletedEventHandlerArgs<TestKafkaRecord>>();
        var worker2Completions = new ConcurrentQueue<PipelineRecordCompletedEventHandlerArgs<TestKafkaRecord>>();
        var worker1Revocations = new ConcurrentQueue<IReadOnlyList<PipelinePartitionLease>>();

        var pipeline1 = Pipeline<TestKafkaRecord>.New($"{scenarioName}-worker-1")
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "instance-1",
                WorkerId = "worker-1"
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .WithInMemoryDestination("config")
            .Build();

        var pipeline2 = Pipeline<TestKafkaRecord>.New($"{scenarioName}-worker-2")
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                InstanceId = "instance-2",
                WorkerId = "worker-2"
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .WithInMemoryDestination("config")
            .Build();

        pipeline1.OnPipelineRecordCompleted += (_, args) => worker1Completions.Enqueue(args);
        pipeline2.OnPipelineRecordCompleted += (_, args) => worker2Completions.Enqueue(args);
        pipeline1.OnPartitionsRevoked += (_, args) => worker1Revocations.Enqueue(args.Partitions);

        await pipeline1.StartPipelineAsync().ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => pipeline1.GetRuntimeContext().OwnedPartitions.Count == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await pipeline2.StartPipelineAsync().ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () =>
                pipeline1.GetRuntimeContext().OwnedPartitions.Count == 1 &&
                pipeline2.GetRuntimeContext().OwnedPartitions.Count == 1,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        Assert.NotEmpty(worker1Revocations);

        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 0,
            new[] { KafkaPipelineTestHelpers.CreateMessage("p0", "value-0") }).ConfigureAwait(false);
        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 1,
            new[] { KafkaPipelineTestHelpers.CreateMessage("p1", "value-1") }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => worker1Completions.Count + worker2Completions.Count == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        var firstWave = worker1Completions.Concat(worker2Completions).ToArray();
        Assert.Equal(2, firstWave.Length);
        Assert.Equal(new int?[] { 0, 1 }, firstWave.Select(args => args.Distribution!.PartitionId).OrderBy(value => value).ToArray());
        Assert.Equal(2, firstWave.Select(args => args.Distribution!.WorkerId).Distinct().Count());

        await pipeline2.CompleteAsync().ConfigureAwait(false);
        await pipeline2.Completion.ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => pipeline1.GetRuntimeContext().OwnedPartitions.Count == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 0,
            new[] { KafkaPipelineTestHelpers.CreateMessage("p0-second", "value-2") }).ConfigureAwait(false);
        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 1,
            new[] { KafkaPipelineTestHelpers.CreateMessage("p1-second", "value-3") }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => worker1Completions.Count(args => args.Record.Key is "p0-second" or "p1-second") == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        var secondWave = worker1Completions
            .Where(args => args.Record.Key is "p0-second" or "p1-second")
            .ToArray();

        Assert.Equal(2, secondWave.Length);
        Assert.All(secondWave, args => Assert.Equal("worker-1", args.Distribution!.WorkerId));
        Assert.Equal(new int?[] { 0, 1 }, secondWave.Select(args => args.Distribution!.PartitionId).OrderBy(value => value).ToArray());
        Assert.Single(worker2Completions);

        await pipeline1.CompleteAsync().ConfigureAwait(false);
        await pipeline1.Completion.ConfigureAwait(false);

        Assert.Equal(PipelineExecutionStatus.Completed, pipeline1.GetStatus().Status);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline2.GetStatus().Status);
    }
}

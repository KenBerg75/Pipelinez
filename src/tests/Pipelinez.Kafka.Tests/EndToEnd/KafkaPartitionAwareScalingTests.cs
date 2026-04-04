using System.Collections.Concurrent;
using Pipelinez.Core;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Performance;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPartitionAwareScalingTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_PreservePartitionOrder_While_Allowing_CrossPartition_Progress()
    {
        var scenarioName = nameof(KafkaPipeline_PreservePartitionOrder_While_Allowing_CrossPartition_Progress);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source", partitions: 2).ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);
        var completions = new ConcurrentQueue<(string Key, int? PartitionId)>();

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                WorkerId = "worker-order"
            })
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                DefaultSegmentExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 32,
                    DegreeOfParallelism = 4,
                    EnsureOrdered = false
                }
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                },
                new KafkaPartitionScalingOptions
                {
                    ExecutionMode = KafkaPartitionExecutionMode.ParallelizeAcrossPartitions,
                    MaxConcurrentPartitions = 2,
                    MaxInFlightPerPartition = 1
                })
            .AddSegment(new PartitionDelaySegment(), new object())
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) =>
            completions.Enqueue((args.Record.Key, args.Distribution?.PartitionId));

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => pipeline.GetRuntimeContext().OwnedPartitions.Count == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 0,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("p0-1", "slow"),
                KafkaPipelineTestHelpers.CreateMessage("p0-2", "fast")
            }).ConfigureAwait(false);
        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 1,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("p1-1", "fast")
            }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => completions.Count == 3,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var completionOrder = completions.ToArray();
        var partition0Order = completionOrder
            .Where(completion => completion.PartitionId == 0)
            .Select(completion => completion.Key)
            .ToArray();

        Assert.Equal(new[] { "p0-1", "p0-2" }, partition0Order);
        Assert.Contains(completionOrder, completion => completion.Key == "p1-1");
        Assert.True(Array.IndexOf(completionOrder.Select(c => c.Key).ToArray(), "p1-1") <
                    Array.IndexOf(completionOrder.Select(c => c.Key).ToArray(), "p0-2"));
    }

    [Fact]
    public async Task KafkaPipeline_RelaxedWithinPartition_Can_Complete_Out_Of_Order_When_Enabled()
    {
        var scenarioName = nameof(KafkaPipeline_RelaxedWithinPartition_Can_Complete_Out_Of_Order_When_Enabled);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source", partitions: 1).ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);
        var completions = new ConcurrentQueue<string>();

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .UseHostOptions(new PipelineHostOptions
            {
                ExecutionMode = PipelineExecutionMode.Distributed,
                WorkerId = "worker-relaxed"
            })
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                DefaultSegmentExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 32,
                    DegreeOfParallelism = 4,
                    EnsureOrdered = false
                }
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                },
                new KafkaPartitionScalingOptions
                {
                    ExecutionMode = KafkaPartitionExecutionMode.RelaxOrderingWithinPartition,
                    MaxConcurrentPartitions = 1,
                    MaxInFlightPerPartition = 2
                })
            .AddSegment(new PartitionDelaySegment(), new object())
            .WithInMemoryDestination("config")
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) => completions.Enqueue(args.Record.Key);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => pipeline.GetRuntimeContext().OwnedPartitions.Count == 1,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await cluster.ProduceToPartitionAsync(
            sourceTopic,
            partition: 0,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("p0-slow", "slow"),
                KafkaPipelineTestHelpers.CreateMessage("p0-fast", "fast")
            }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => completions.Count == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Equal(new[] { "p0-fast", "p0-slow" }, completions.ToArray());
    }
}

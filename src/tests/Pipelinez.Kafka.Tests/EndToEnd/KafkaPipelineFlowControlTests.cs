using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Status;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPipelineFlowControlTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_DownstreamPressure_Slows_Ingress_Without_Faulting()
    {
        var scenarioName = nameof(KafkaPipeline_DownstreamPressure_Slows_Ingress_Without_Faulting);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);
        var destination = new BlockingCountingDestination();

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .UsePerformanceOptions(new PipelinePerformanceOptions
            {
                SourceExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 1,
                    DegreeOfParallelism = 1,
                    EnsureOrdered = true
                },
                DestinationExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 1,
                    DegreeOfParallelism = 1,
                    EnsureOrdered = true
                }
            })
            .UseFlowControlOptions(new PipelineFlowControlOptions
            {
                OverflowPolicy = PipelineOverflowPolicy.Wait,
                SaturationWarningThreshold = 0.5d
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .WithDestination(destination)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("key-1", "value-1"),
                KafkaPipelineTestHelpers.CreateMessage("key-2", "value-2"),
                KafkaPipelineTestHelpers.CreateMessage("key-3", "value-3"),
                KafkaPipelineTestHelpers.CreateMessage("key-4", "value-4")
            }).ConfigureAwait(false);

        await destination.FirstExecutionStarted.Task.WaitAsync(cluster.ConsumeTimeout).ConfigureAwait(false);
        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => pipeline.GetStatus().FlowControlStatus is { TotalBufferedCount: >= 2 },
            cluster.ConsumeTimeout).ConfigureAwait(false);

        var activeStatus = pipeline.GetStatus();
        Assert.NotNull(activeStatus.FlowControlStatus);
        Assert.NotEqual(PipelineExecutionStatus.Faulted, activeStatus.Status);
        Assert.True(activeStatus.FlowControlStatus!.SaturationRatio > 0);
        Assert.True(activeStatus.FlowControlStatus.TotalBufferedCount >= 2);

        destination.ReleaseFirstExecution();

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => destination.ProcessedKeys.Count == 4,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.True(snapshot.TotalPublishWaitCount > 0);
        Assert.Equal(0, snapshot.TotalPublishRejectedCount);
        Assert.True(snapshot.PeakBufferedCount > 0);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }
}

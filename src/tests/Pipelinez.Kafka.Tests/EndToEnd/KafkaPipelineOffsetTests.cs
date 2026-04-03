using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Status;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPipelineOffsetTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_Successful_Offsets_Are_Reused_And_Prevent_Replay()
    {
        var scenarioName = nameof(KafkaPipeline_Successful_Offsets_Are_Reused_And_Prevent_Replay);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var firstDestinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination-1").ConfigureAwait(false);
        var secondDestinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination-2").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("first", "one"),
                KafkaPipelineTestHelpers.CreateMessage("second", "two")
            }).ConfigureAwait(false);

        var firstPipeline = CreateKafkaPipeline(
            $"{scenarioName}-run-1",
            sourceTopic,
            firstDestinationTopic,
            consumerGroup);

        await firstPipeline.StartPipelineAsync().ConfigureAwait(false);

        var firstRunMessages = await cluster
            .ConsumeAsync(
                firstDestinationTopic,
                expectedCount: 2,
                consumerGroup: KafkaTopicNameFactory.CreateConsumerGroupName("probe", $"{scenarioName}-run-1"))
            .ConfigureAwait(false);

        var committedOffset = await cluster
            .WaitForCommittedOffsetAsync(sourceTopic, consumerGroup, minimumOffset: 2)
            .ConfigureAwait(false);

        await firstPipeline.CompleteAsync().ConfigureAwait(false);
        await firstPipeline.Completion.ConfigureAwait(false);

        var secondPipeline = CreateKafkaPipeline(
            $"{scenarioName}-run-2",
            sourceTopic,
            secondDestinationTopic,
            consumerGroup);

        await secondPipeline.StartPipelineAsync().ConfigureAwait(false);

        var replayedMessages = await cluster
            .ConsumeAvailableAsync(
                secondDestinationTopic,
                KafkaTopicNameFactory.CreateConsumerGroupName("probe", $"{scenarioName}-run-2"),
                observationWindow: TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        await secondPipeline.CompleteAsync().ConfigureAwait(false);
        await secondPipeline.Completion.ConfigureAwait(false);

        Assert.Equal(2, firstRunMessages.Count);
        Assert.Equal(2, committedOffset);
        Assert.Empty(replayedMessages);
    }

    [Fact]
    public async Task KafkaPipeline_Faulted_Record_Does_Not_Advance_Committed_Offset()
    {
        var scenarioName = nameof(KafkaPipeline_Faulted_Record_Does_Not_Advance_Committed_Offset);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var destinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .AddSegment(new ConditionalFaultingKafkaSegment("bad"), new object())
            .WithKafkaDestination(
                cluster.CreateDestinationOptions(destinationTopic),
                (TestKafkaRecord record) => new Message<string, string>
                {
                    Key = record.Key,
                    Value = record.Value
                })
            .WithErrorHandler(_ => PipelineErrorAction.StopPipeline)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("bad-1", "bad")
            }).ConfigureAwait(false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion.ConfigureAwait(false));

        var committedOffset = cluster.GetCommittedOffset(sourceTopic, consumerGroup);

        Assert.Null(committedOffset);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }

    private IPipeline<TestKafkaRecord> CreateKafkaPipeline(
        string pipelineName,
        string sourceTopic,
        string destinationTopic,
        string consumerGroup)
    {
        return Pipeline<TestKafkaRecord>.New(pipelineName)
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .WithKafkaDestination(
                cluster.CreateDestinationOptions(destinationTopic),
                (TestKafkaRecord record) => new Message<string, string>
                {
                    Key = record.Key,
                    Value = record.Value
                })
            .Build();
    }
}

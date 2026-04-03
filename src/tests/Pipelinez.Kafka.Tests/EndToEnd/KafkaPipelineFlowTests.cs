using System.Collections.Concurrent;
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
public sealed class KafkaPipelineFlowTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_Processes_Records_EndToEnd_And_Preserves_Headers()
    {
        var scenarioName = nameof(KafkaPipeline_Processes_Records_EndToEnd_And_Preserves_Headers);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var destinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        var completedRecords = new ConcurrentQueue<TestKafkaRecord>();

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .AddSegment(new AppendValueSegment("|segment"), new object())
            .AddSegment(new AddHeaderSegment("processed-by", "pipelinez"), new object())
            .WithKafkaDestination(
                cluster.CreateDestinationOptions(destinationTopic),
                (TestKafkaRecord record) => new Message<string, string>
                {
                    Key = record.Key,
                    Value = record.Value
                })
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) => completedRecords.Enqueue(args.Record);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("alpha", "value-1", ("source-header", "source-value"))
            }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => completedRecords.Count == 1,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        var destinationMessages = await cluster
            .ConsumeAsync(
                destinationTopic,
                expectedCount: 1,
                consumerGroup: KafkaTopicNameFactory.CreateConsumerGroupName("probe", scenarioName))
            .ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        var destinationMessage = Assert.Single(destinationMessages).Message;
        Assert.Equal("alpha", destinationMessage.Key);
        Assert.Equal("value-1|segment", destinationMessage.Value);

        var headers = destinationMessage.Headers.ToDictionary(
            header => header.Key,
            header => System.Text.Encoding.UTF8.GetString(header.GetValueBytes()));

        Assert.Equal("source-value", headers["source-header"]);
        Assert.Equal("pipelinez", headers["processed-by"]);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);

        var completedRecord = Assert.Single(completedRecords);
        Assert.Equal("value-1|segment", completedRecord.Value);
    }

    [Fact]
    public async Task KafkaPipeline_SkipRecord_Continues_And_Raises_RecordFaulted()
    {
        var scenarioName = nameof(KafkaPipeline_SkipRecord_Continues_And_Raises_RecordFaulted);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var destinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        var completedKeys = new ConcurrentQueue<string>();
        var faultedKeys = new ConcurrentQueue<string>();
        var pipelineFaulted = false;

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .AddSegment(new ConditionalFaultingKafkaSegment("bad"), new object())
            .AddSegment(new AppendValueSegment("|ok"), new object())
            .WithKafkaDestination(
                cluster.CreateDestinationOptions(destinationTopic),
                (TestKafkaRecord record) => new Message<string, string>
                {
                    Key = record.Key,
                    Value = record.Value
                })
            .WithErrorHandler(_ => PipelineErrorAction.SkipRecord)
            .Build();

        pipeline.OnPipelineRecordCompleted += (_, args) => completedKeys.Enqueue(args.Record.Key);
        pipeline.OnPipelineRecordFaulted += (_, args) => faultedKeys.Enqueue(args.Record.Key);
        pipeline.OnPipelineFaulted += (_, _) => pipelineFaulted = true;

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("good-1", "first"),
                KafkaPipelineTestHelpers.CreateMessage("bad-1", "bad"),
                KafkaPipelineTestHelpers.CreateMessage("good-2", "second")
            }).ConfigureAwait(false);

        var outputMessages = await cluster
            .ConsumeAsync(
                destinationTopic,
                expectedCount: 2,
                consumerGroup: KafkaTopicNameFactory.CreateConsumerGroupName("probe", scenarioName))
            .ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => faultedKeys.Count == 1 && completedKeys.Count == 2,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Equal(new[] { "good-1", "good-2" }, outputMessages.Select(message => message.Message.Key).ToArray());
        Assert.Equal(new[] { "good-1", "good-2" }, completedKeys.ToArray());
        Assert.Equal(new[] { "bad-1" }, faultedKeys.ToArray());
        Assert.False(pipelineFaulted);
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task KafkaPipeline_DestinationFault_SkipRecord_Continues_Processing()
    {
        var scenarioName = nameof(KafkaPipeline_DestinationFault_SkipRecord_Continues_Processing);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);
        var destination = new FaultInjectingDestination("bad");

        var faultedKeys = new ConcurrentQueue<string>();

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .WithDestination(destination)
            .WithErrorHandler(_ => PipelineErrorAction.SkipRecord)
            .Build();

        pipeline.OnPipelineRecordFaulted += (_, args) => faultedKeys.Enqueue(args.Record.Key);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("good-1", "good"),
                KafkaPipelineTestHelpers.CreateMessage("bad-1", "bad"),
                KafkaPipelineTestHelpers.CreateMessage("good-2", "great")
            }).ConfigureAwait(false);

        await KafkaPipelineTestHelpers.WaitForConditionAsync(
            () => destination.ProcessedKeys.Count == 2 && faultedKeys.Count == 1,
            cluster.ConsumeTimeout).ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Equal(new[] { "good-1", "good-2" }, destination.ProcessedKeys.ToArray());
        Assert.Equal(new[] { "bad-1" }, faultedKeys.ToArray());
        Assert.Equal(PipelineExecutionStatus.Completed, pipeline.GetStatus().Status);
    }
}

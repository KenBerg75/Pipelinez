using System.Text.Json;
using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Operational;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Record;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPipelineDeadLetterTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_DeadLetter_Publishes_Faulted_Record_To_DeadLetter_Topic_And_Continues()
    {
        var scenarioName = nameof(KafkaPipeline_DeadLetter_Publishes_Faulted_Record_To_DeadLetter_Topic_And_Continues);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var destinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination").ConfigureAwait(false);
        var deadLetterTopic = await cluster.CreateTopicAsync($"{scenarioName}-deadletter").ConfigureAwait(false);
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
            .WithKafkaDeadLetterDestination(
                cluster.CreateDestinationOptions(deadLetterTopic),
                deadLetter => new Message<string, string>
                {
                    Key = deadLetter.Record.Key,
                    Value = JsonSerializer.Serialize(new
                    {
                        key = deadLetter.Record.Key,
                        value = deadLetter.Record.Value,
                        component = deadLetter.Fault.ComponentName,
                        retryCount = deadLetter.RetryHistory.Count,
                        correlationId = deadLetter.Metadata.GetValue(PipelineOperationalMetadataKeys.CorrelationId),
                        topic = deadLetter.Metadata.GetValue(KafkaMetadataKeys.SOURCE_TOPIC_NAME),
                        partition = deadLetter.Metadata.GetValue(KafkaMetadataKeys.SOURCE_PARTITION),
                        offset = deadLetter.Metadata.GetValue(KafkaMetadataKeys.SOURCE_OFFSET)
                    })
                })
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("bad-1", "bad"),
                KafkaPipelineTestHelpers.CreateMessage("good-1", "good")
            }).ConfigureAwait(false);

        var outputMessages = await cluster
            .ConsumeAsync(
                destinationTopic,
                expectedCount: 1,
                consumerGroup: KafkaTopicNameFactory.CreateConsumerGroupName("probe", $"{scenarioName}-destination"))
            .ConfigureAwait(false);

        var deadLetterMessages = await cluster
            .ConsumeAsync(
                deadLetterTopic,
                expectedCount: 1,
                consumerGroup: KafkaTopicNameFactory.CreateConsumerGroupName("probe", $"{scenarioName}-deadletter"))
            .ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Single(outputMessages);
        Assert.Equal("good-1", outputMessages[0].Message.Key);
        Assert.Equal("good", outputMessages[0].Message.Value);

        var deadLetterMessage = Assert.Single(deadLetterMessages);
        Assert.Equal("bad-1", deadLetterMessage.Message.Key);

        Assert.Contains(
            pipeline.GetHealthStatus().Reasons,
            reason => reason.Contains("Dead-letter", StringComparison.OrdinalIgnoreCase));

        using var document = JsonDocument.Parse(deadLetterMessage.Message.Value);
        var root = document.RootElement;
        Assert.Equal("bad-1", root.GetProperty("key").GetString());
        Assert.Equal("bad", root.GetProperty("value").GetString());
        Assert.Equal(nameof(ConditionalFaultingKafkaSegment), root.GetProperty("component").GetString());
        Assert.Equal(0, root.GetProperty("retryCount").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
        Assert.Equal(sourceTopic, root.GetProperty("topic").GetString());
        Assert.Equal("0", root.GetProperty("partition").GetString());
        Assert.Equal("0", root.GetProperty("offset").GetString());

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.TotalDeadLetteredCount);
        Assert.Equal(0, snapshot.TotalDeadLetterFailureCount);
        Assert.Equal(1, snapshot.TotalRecordsCompleted);
        Assert.Equal(1, snapshot.TotalRecordsFaulted);
    }
}

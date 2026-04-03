using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Retry;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPipelineRetryTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_Segment_Retry_Recovers_And_Produces_Output()
    {
        var scenarioName = nameof(KafkaPipeline_Segment_Retry_Recovers_And_Produces_Output);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var destinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);
        var retryEvents = new List<PipelineRecordRetryingEventArgs<TestKafkaRecord>>();

        var pipeline = Pipeline<TestKafkaRecord>.New(scenarioName)
            .UseRetryOptions(new PipelineRetryOptions<TestKafkaRecord>
            {
                DefaultSegmentPolicy = PipelineRetryPolicy<TestKafkaRecord>
                    .FixedDelay(3, TimeSpan.Zero)
                    .Handle<InvalidOperationException>()
            })
            .WithKafkaSource(
                cluster.CreateSourceOptions(sourceTopic, consumerGroup),
                (string key, string value) => new TestKafkaRecord
                {
                    Key = key,
                    Value = value
                })
            .AddSegment(new FlakyKafkaSegment(), new object())
            .WithKafkaDestination(
                cluster.CreateDestinationOptions(destinationTopic),
                (TestKafkaRecord record) => new Message<string, string>
                {
                    Key = record.Key,
                    Value = record.Value
                })
            .Build();

        pipeline.OnPipelineRecordRetrying += (_, args) => retryEvents.Add(args);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("retry-1", "value")
            }).ConfigureAwait(false);

        var outputMessages = await cluster
            .ConsumeAsync(
                destinationTopic,
                expectedCount: 1,
                consumerGroup: KafkaTopicNameFactory.CreateConsumerGroupName("probe", scenarioName))
            .ConfigureAwait(false);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);

        Assert.Single(retryEvents);
        Assert.Single(outputMessages);
        Assert.Equal("retry-1", outputMessages[0].Message.Key);
        Assert.Equal("value|retried", outputMessages[0].Message.Value);

        var snapshot = pipeline.GetPerformanceSnapshot();
        Assert.Equal(1, snapshot.TotalRetryCount);
        Assert.Equal(1, snapshot.SuccessfulRetryRecoveries);
        Assert.Equal(0, snapshot.RetryExhaustions);
    }
}

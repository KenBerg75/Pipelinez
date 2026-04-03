using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Status;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Tests.Infrastructure;
using Pipelinez.Kafka.Tests.Models;
using Xunit;

namespace Pipelinez.Kafka.Tests.EndToEnd;

[Collection(KafkaIntegrationCollection.Name)]
public sealed class KafkaPipelineFaultTests(KafkaTestCluster cluster)
{
    [Fact]
    public async Task KafkaPipeline_StopPipeline_Faults_And_Prevents_Later_Output()
    {
        var scenarioName = nameof(KafkaPipeline_StopPipeline_Faults_And_Prevents_Later_Output);
        var sourceTopic = await cluster.CreateTopicAsync($"{scenarioName}-source").ConfigureAwait(false);
        var destinationTopic = await cluster.CreateTopicAsync($"{scenarioName}-destination").ConfigureAwait(false);
        var consumerGroup = KafkaTopicNameFactory.CreateConsumerGroupName("pipelinez", scenarioName);

        PipelineFaultedEventArgs? pipelineFault = null;
        PipelineRecordFaultedEventArgs<TestKafkaRecord>? recordFault = null;

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

        pipeline.OnPipelineRecordFaulted += (_, args) => recordFault = args;
        pipeline.OnPipelineFaulted += (_, args) => pipelineFault = args;

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("bad-1", "bad"),
                KafkaPipelineTestHelpers.CreateMessage("good-1", "good")
            }).ConfigureAwait(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion.ConfigureAwait(false));

        var outputMessages = await cluster
            .ConsumeAvailableAsync(
                destinationTopic,
                KafkaTopicNameFactory.CreateConsumerGroupName("probe", scenarioName))
            .ConfigureAwait(false);

        Assert.Equal(ConditionalFaultingKafkaSegment.DefaultFailureMessage, exception.Message);
        Assert.NotNull(recordFault);
        Assert.NotNull(pipelineFault);
        Assert.Equal("bad-1", recordFault!.Record.Key);
        Assert.Equal("ConditionalFaultingKafkaSegment", pipelineFault!.ComponentName);
        Assert.Empty(outputMessages);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }

    [Fact]
    public async Task KafkaPipeline_Rethrow_Preserves_Original_Exception()
    {
        var scenarioName = nameof(KafkaPipeline_Rethrow_Preserves_Original_Exception);
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
            .WithErrorHandler(_ => PipelineErrorAction.Rethrow)
            .Build();

        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        await cluster.ProduceAsync(
            sourceTopic,
            new[]
            {
                KafkaPipelineTestHelpers.CreateMessage("bad-1", "bad")
            }).ConfigureAwait(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await pipeline.Completion.ConfigureAwait(false));

        var outputMessages = await cluster
            .ConsumeAvailableAsync(
                destinationTopic,
                KafkaTopicNameFactory.CreateConsumerGroupName("probe", scenarioName))
            .ConfigureAwait(false);

        Assert.Equal(ConditionalFaultingKafkaSegment.DefaultFailureMessage, exception.Message);
        Assert.Empty(outputMessages);
        Assert.Equal(PipelineExecutionStatus.Faulted, pipeline.GetStatus().Status);
    }
}

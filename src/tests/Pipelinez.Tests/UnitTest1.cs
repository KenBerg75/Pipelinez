using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Kafka;
using Pipelinez.Kafka.Configuration;

namespace Pipelinez.Tests;

public class UnitTest1
{
    [Fact]
    public void PipelineBuilder_Does_Not_Expose_Legacy_String_Based_Kafka_Overloads()
    {
        var publicMethods = typeof(PipelineBuilder<>).GetMethods();

        var legacyKafkaOverloads = publicMethods
            .Where(method =>
                method.Name is "WithKafkaSource" or "WithKafkaDestination" &&
                method.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))
            .ToList();

        Assert.Empty(legacyKafkaOverloads);
    }

    [Fact]
    public void Kafka_Builder_Extensions_Are_Exposed_From_Kafka_Assembly()
    {
        var builder = Pipeline<SomePipelineRecordClass>.New("KafkaBuilderExtensions")
            .WithKafkaSource(
                new KafkaSourceOptions(),
                (string key, string value) => new SomePipelineRecordClass
                {
                    JobPosting = value,
                    Key = key,
                    CSJobPosting = value
                })
            .WithKafkaDestination(
                new KafkaDestinationOptions(),
                (SomePipelineRecordClass record) => new Message<string, string>
                {
                    Key = record.Key,
                    Value = record.JobPosting
                });

        Assert.NotNull(builder);
    }
}

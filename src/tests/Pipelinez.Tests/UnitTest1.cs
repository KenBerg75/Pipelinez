using Confluent.Kafka;
using Pipelinez.Core;

namespace Pipelinez.Tests;

public class UnitTest1
{
    //[Fact]
    public async void Pipeline_Build()
    {
        /*var pipeline1 = Pipeline<SomePipelineRecordClass>.New("SomeAppName")
            .WithInLineSource("config")
            //.AddSegment(new SomeSegment(), "config")
            .WithInLineDestination("config")
            .Build();

        await pipeline1.StartAsync(token);*/
        
        //pipeline1.Publish();

        /*string config = "config";

        var pipeline = Pipeline<SomePipelineRecordClass>.New("SomeOtherAppName")
            .WithKafkaSource<string, string>(config, (key, value) =>
            {
                return new SomePipelineRecordClass()
                {
                    Key = key,
                    JobPosting = value
                };
            })
            .AddSegment(new SomeSegment(), "config")
            .WithKafkaDestination<string, string>(config, record =>
            {
                return new Message<string, string>()
                {
                    Key = record.Key,
                    Value = record.CSJobPosting
                };
                // Can't set the delivery handler here, might need to abstract a KafkaMessage out.
            })
            .Build();

        await pipeline.StartAsync();*/
    }
}

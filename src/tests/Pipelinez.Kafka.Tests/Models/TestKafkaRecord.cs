using Pipelinez.Core.Record;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class TestKafkaRecord : PipelineRecord
{
    public required string Key { get; init; }

    public required string Value { get; set; }
}

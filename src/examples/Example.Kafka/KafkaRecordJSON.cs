using Pipelinez.Core.Record;

namespace Example.Kafka;

public class KafkaRecordJSON : PipelineRecord
{
    public required string RecordKey { get; set; }
    public required string RecordValue { get; set; }
}

using Pipelinez.Core.Record;

namespace Example.Kafka;

public class KafkaRecordJSON : PipelineRecord
{
    public string RecordKey { get; set; }
    public string RecordValue { get; set; }
}
using Confluent.Kafka;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Record;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Destination;
using Pipelinez.Kafka.Source;

// Must be in the same namespace as the PipelineBuilder class (partial_classes)
namespace Pipelinez.Core;

public partial class PipelineBuilder<T> where T : PipelineRecord
{
    public PipelineBuilder<T> WithKafkaSource<TRecordKey, TRecordValue>(KafkaSourceOptions config,
        Func<TRecordKey, TRecordValue, T> recordMapper) where TRecordKey : class where TRecordValue : class
    {
        _source = new KafkaPipelineSource<T, TRecordKey, TRecordValue>(pipelineName, config, recordMapper);
        return this;
    }
    
    public PipelineBuilder<T> WithKafkaDestination<TRecordKey, TRecordValue>(KafkaDestinationOptions config,
        Func<T, Message<TRecordKey, TRecordValue>> recordMapper) where TRecordKey : class where TRecordValue : class
    {
        _destination = new KafkaPipelineDestination<T, TRecordKey, TRecordValue>(pipelineName, config, recordMapper);
        return this;
    }
}
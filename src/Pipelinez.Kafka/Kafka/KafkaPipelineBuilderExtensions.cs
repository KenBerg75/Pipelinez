using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.Record;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Destination;
using Pipelinez.Kafka.Source;

namespace Pipelinez.Kafka;

public static class KafkaPipelineBuilderExtensions
{
    public static PipelineBuilder<T> WithKafkaSource<T, TRecordKey, TRecordValue>(
        this PipelineBuilder<T> builder,
        KafkaSourceOptions config,
        Func<TRecordKey, TRecordValue, T> recordMapper) where TRecordKey : class where TRecordValue : class
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(recordMapper);

        return builder.WithSource(new KafkaPipelineSource<T, TRecordKey, TRecordValue>(
            builder.PipelineName,
            config,
            recordMapper));
    }
    
    public static PipelineBuilder<T> WithKafkaDestination<T, TRecordKey, TRecordValue>(
        this PipelineBuilder<T> builder,
        KafkaDestinationOptions config,
        Func<T, Message<TRecordKey, TRecordValue>> recordMapper) where TRecordKey : class where TRecordValue : class
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(recordMapper);

        return builder.WithDestination(new KafkaPipelineDestination<T, TRecordKey, TRecordValue>(
            builder.PipelineName,
            config,
            recordMapper));
    }
}

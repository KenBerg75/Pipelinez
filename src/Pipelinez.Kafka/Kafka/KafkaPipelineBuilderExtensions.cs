using Confluent.Kafka;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;
using Pipelinez.Kafka.Configuration;
using Pipelinez.Kafka.Destination;
using Pipelinez.Kafka.Source;

namespace Pipelinez.Kafka;

/// <summary>
/// Provides Kafka transport extension methods for <see cref="PipelineBuilder{T}" />.
/// </summary>
public static class KafkaPipelineBuilderExtensions
{
    /// <summary>
    /// Configures the pipeline to consume records from Kafka.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <typeparam name="TRecordKey">The Kafka key type.</typeparam>
    /// <typeparam name="TRecordValue">The Kafka value type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="config">The Kafka source configuration.</param>
    /// <param name="recordMapper">Maps the Kafka key and value into a pipeline record.</param>
    /// <returns>The same builder for chaining.</returns>
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

    /// <summary>
    /// Configures the pipeline to consume records from Kafka with explicit partition scaling behavior.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <typeparam name="TRecordKey">The Kafka key type.</typeparam>
    /// <typeparam name="TRecordValue">The Kafka value type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="config">The Kafka source configuration.</param>
    /// <param name="recordMapper">Maps the Kafka key and value into a pipeline record.</param>
    /// <param name="partitionScalingOptions">The partition-aware execution settings to apply.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithKafkaSource<T, TRecordKey, TRecordValue>(
        this PipelineBuilder<T> builder,
        KafkaSourceOptions config,
        Func<TRecordKey, TRecordValue, T> recordMapper,
        KafkaPartitionScalingOptions partitionScalingOptions) where TRecordKey : class where TRecordValue : class
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(recordMapper);
        ArgumentNullException.ThrowIfNull(partitionScalingOptions);

        config.PartitionScaling = partitionScalingOptions.Validate();
        return builder.WithKafkaSource<T, TRecordKey, TRecordValue>(config, recordMapper);
    }

    /// <summary>
    /// Configures the pipeline to publish records to Kafka.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <typeparam name="TRecordKey">The Kafka key type.</typeparam>
    /// <typeparam name="TRecordValue">The Kafka value type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="config">The Kafka destination configuration.</param>
    /// <param name="recordMapper">Maps a pipeline record to the Kafka message to publish.</param>
    /// <returns>The same builder for chaining.</returns>
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

    /// <summary>
    /// Configures the pipeline to write dead-letter records to Kafka.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <typeparam name="TRecordKey">The Kafka key type.</typeparam>
    /// <typeparam name="TRecordValue">The Kafka value type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="config">The Kafka destination configuration.</param>
    /// <param name="recordMapper">Maps the dead-letter envelope into the Kafka message to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithKafkaDeadLetterDestination<T, TRecordKey, TRecordValue>(
        this PipelineBuilder<T> builder,
        KafkaDestinationOptions config,
        Func<PipelineDeadLetterRecord<T>, Message<TRecordKey, TRecordValue>> recordMapper)
        where TRecordKey : class
        where TRecordValue : class
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(recordMapper);

        return builder.WithDeadLetterDestination(new KafkaDeadLetterDestination<T, TRecordKey, TRecordValue>(
            builder.PipelineName,
            config,
            recordMapper));
    }
}

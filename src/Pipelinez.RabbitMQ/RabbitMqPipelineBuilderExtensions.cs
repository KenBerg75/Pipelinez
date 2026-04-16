using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;
using Pipelinez.RabbitMQ.Configuration;
using Pipelinez.RabbitMQ.DeadLettering;
using Pipelinez.RabbitMQ.Destination;
using Pipelinez.RabbitMQ.Source;

namespace Pipelinez.RabbitMQ;

/// <summary>
/// Provides RabbitMQ transport extension methods for <see cref="PipelineBuilder{T}" />.
/// </summary>
public static class RabbitMqPipelineBuilderExtensions
{
    /// <summary>
    /// Configures the pipeline to consume records from a RabbitMQ queue.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The RabbitMQ source options.</param>
    /// <param name="recordMapper">Maps a RabbitMQ delivery into a pipeline record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithRabbitMqSource<T>(
        this PipelineBuilder<T> builder,
        RabbitMqSourceOptions options,
        Func<RabbitMqDeliveryContext, T> recordMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(recordMapper);

        return builder.WithSource(new RabbitMqPipelineSource<T>(
            options.Validate(),
            recordMapper));
    }

    /// <summary>
    /// Configures the pipeline to publish records to RabbitMQ.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The RabbitMQ destination options.</param>
    /// <param name="messageMapper">Maps a pipeline record to the RabbitMQ message to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithRabbitMqDestination<T>(
        this PipelineBuilder<T> builder,
        RabbitMqDestinationOptions options,
        Func<T, RabbitMqPublishMessage> messageMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messageMapper);

        return builder.WithDestination(new RabbitMqPipelineDestination<T>(
            options.Validate(),
            messageMapper));
    }

    /// <summary>
    /// Configures the pipeline to write Pipelinez dead-letter records to RabbitMQ.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The RabbitMQ dead-letter destination options.</param>
    /// <param name="messageMapper">Maps the dead-letter envelope into the RabbitMQ message to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithRabbitMqDeadLetterDestination<T>(
        this PipelineBuilder<T> builder,
        RabbitMqDeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, RabbitMqPublishMessage> messageMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messageMapper);

        return builder.WithDeadLetterDestination(new RabbitMqDeadLetterDestination<T>(
            options,
            messageMapper));
    }
}

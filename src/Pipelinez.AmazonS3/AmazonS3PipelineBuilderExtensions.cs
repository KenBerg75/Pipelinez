using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.DeadLettering;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.AmazonS3.Source;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Record;

namespace Pipelinez.AmazonS3;

/// <summary>
/// Provides Amazon S3 transport extension methods for <see cref="PipelineBuilder{T}" />.
/// </summary>
public static class AmazonS3PipelineBuilderExtensions
{
    /// <summary>
    /// Configures the pipeline to enumerate records from Amazon S3 objects.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Amazon S3 source options.</param>
    /// <param name="recordMapper">Maps an S3 object context into a pipeline record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAmazonS3Source<T>(
        this PipelineBuilder<T> builder,
        AmazonS3SourceOptions options,
        Func<AmazonS3ObjectContext, CancellationToken, Task<T>> recordMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(recordMapper);

        return builder.WithSource(new AmazonS3PipelineSource<T>(
            options.Validate(),
            recordMapper));
    }

    /// <summary>
    /// Configures the pipeline to enumerate records from Amazon S3 objects.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Amazon S3 source options.</param>
    /// <param name="recordMapper">Maps an S3 object context into a pipeline record.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAmazonS3Source<T>(
        this PipelineBuilder<T> builder,
        AmazonS3SourceOptions options,
        Func<AmazonS3ObjectContext, T> recordMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(recordMapper);
        return builder.WithAmazonS3Source(
            options,
            (context, _) => Task.FromResult(recordMapper(context)));
    }

    /// <summary>
    /// Configures the pipeline to publish records as Amazon S3 objects.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Amazon S3 destination options.</param>
    /// <param name="objectMapper">Maps a pipeline record to the S3 object to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAmazonS3Destination<T>(
        this PipelineBuilder<T> builder,
        AmazonS3DestinationOptions options,
        Func<T, AmazonS3PutObject> objectMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(objectMapper);

        return builder.WithDestination(new AmazonS3PipelineDestination<T>(
            options.Validate(),
            objectMapper));
    }

    /// <summary>
    /// Configures the pipeline to write Pipelinez dead-letter records as Amazon S3 objects.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Amazon S3 dead-letter destination options.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAmazonS3DeadLetterDestination<T>(
        this PipelineBuilder<T> builder,
        AmazonS3DeadLetterOptions options)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.WithDeadLetterDestination(new AmazonS3DeadLetterDestination<T>(options));
    }

    /// <summary>
    /// Configures the pipeline to write Pipelinez dead-letter records as Amazon S3 objects.
    /// </summary>
    /// <typeparam name="T">The pipeline record type.</typeparam>
    /// <param name="builder">The pipeline builder to configure.</param>
    /// <param name="options">The Amazon S3 dead-letter destination options.</param>
    /// <param name="objectMapper">Maps a dead-letter envelope to the S3 object to publish.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PipelineBuilder<T> WithAmazonS3DeadLetterDestination<T>(
        this PipelineBuilder<T> builder,
        AmazonS3DeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, AmazonS3PutObject> objectMapper)
        where T : PipelineRecord
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(objectMapper);

        return builder.WithDeadLetterDestination(new AmazonS3DeadLetterDestination<T>(
            options.Validate(),
            objectMapper));
    }
}

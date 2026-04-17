using Microsoft.Extensions.Logging;
using Pipelinez.AmazonS3.Client;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Destination;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.AmazonS3.DeadLettering;

/// <summary>
/// Writes Pipelinez dead-letter records as Amazon S3 objects.
/// </summary>
/// <typeparam name="T">The pipeline record type being dead-lettered.</typeparam>
public sealed class AmazonS3DeadLetterDestination<T> : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
{
    private readonly AmazonS3DeadLetterOptions _options;
    private readonly Func<PipelineDeadLetterRecord<T>, AmazonS3PutObject> _objectMapper;
    private readonly IAmazonS3ClientFactory _clientFactory;
    private readonly ILogger<AmazonS3DeadLetterDestination<T>> _logger;
    private IAmazonS3Client? _client;

    /// <summary>
    /// Initializes a new Amazon S3 dead-letter destination.
    /// </summary>
    /// <param name="options">The dead-letter destination options.</param>
    public AmazonS3DeadLetterDestination(AmazonS3DeadLetterOptions options)
        : this(options, record => AmazonS3DeadLetterObjectMapper.Map(record, options), AmazonS3ClientFactory.Instance)
    {
    }

    /// <summary>
    /// Initializes a new Amazon S3 dead-letter destination.
    /// </summary>
    /// <param name="options">The dead-letter destination options.</param>
    /// <param name="objectMapper">Maps the dead-letter envelope to the S3 object to write.</param>
    public AmazonS3DeadLetterDestination(
        AmazonS3DeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, AmazonS3PutObject> objectMapper)
        : this(options, objectMapper, AmazonS3ClientFactory.Instance)
    {
    }

    internal AmazonS3DeadLetterDestination(
        AmazonS3DeadLetterOptions options,
        IAmazonS3ClientFactory clientFactory)
        : this(options, record => AmazonS3DeadLetterObjectMapper.Map(record, options), clientFactory)
    {
    }

    internal AmazonS3DeadLetterDestination(
        AmazonS3DeadLetterOptions options,
        Func<PipelineDeadLetterRecord<T>, AmazonS3PutObject> objectMapper,
        IAmazonS3ClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _objectMapper = objectMapper ?? throw new ArgumentNullException(nameof(objectMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<AmazonS3DeadLetterDestination<T>>();
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        PipelineDeadLetterRecord<T> deadLetterRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterRecord);

        var putObject = _objectMapper(deadLetterRecord)
                        ?? throw new InvalidOperationException("Amazon S3 dead-letter object mapper returned null.");
        var request = putObject.BuildRequest(_options.BucketName, _options.Write);
        var client = GetOrCreateClient();

        await client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogTrace(
            "Published Pipelinez dead-letter object {ObjectKey} to Amazon S3 bucket {BucketName}.",
            request.Key,
            request.BucketName);
    }

    private IAmazonS3Client GetOrCreateClient()
    {
        return _client ??= _clientFactory.CreateClient(_options.Connection);
    }
}

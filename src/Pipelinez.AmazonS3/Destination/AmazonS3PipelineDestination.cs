using Microsoft.Extensions.Logging;
using Pipelinez.AmazonS3.Client;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.AmazonS3.Destination;

/// <summary>
/// Publishes pipeline records as Amazon S3 objects.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
public sealed class AmazonS3PipelineDestination<T> : PipelineDestination<T>
    where T : PipelineRecord
{
    private readonly AmazonS3DestinationOptions _options;
    private readonly Func<T, AmazonS3PutObject> _objectMapper;
    private readonly IAmazonS3ClientFactory _clientFactory;
    private readonly ILogger<AmazonS3PipelineDestination<T>> _logger;
    private IAmazonS3Client? _client;

    /// <summary>
    /// Initializes a new Amazon S3 destination.
    /// </summary>
    /// <param name="options">The destination options.</param>
    /// <param name="objectMapper">Maps a pipeline record to the S3 object to write.</param>
    public AmazonS3PipelineDestination(
        AmazonS3DestinationOptions options,
        Func<T, AmazonS3PutObject> objectMapper)
        : this(options, objectMapper, AmazonS3ClientFactory.Instance)
    {
    }

    internal AmazonS3PipelineDestination(
        AmazonS3DestinationOptions options,
        Func<T, AmazonS3PutObject> objectMapper,
        IAmazonS3ClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _objectMapper = objectMapper ?? throw new ArgumentNullException(nameof(objectMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<AmazonS3PipelineDestination<T>>();
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        _logger.LogInformation(
            "Initializing Amazon S3 destination for bucket {BucketName}.",
            _options.BucketName);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var putObject = _objectMapper(record)
                        ?? throw new InvalidOperationException("Amazon S3 object mapper returned null.");
        var request = putObject.BuildRequest(_options.BucketName, _options.Write);
        var client = GetOrCreateClient();

        await client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogTrace(
            "Published Amazon S3 object {ObjectKey} to bucket {BucketName}.",
            request.Key,
            request.BucketName);
    }

    private IAmazonS3Client GetOrCreateClient()
    {
        return _client ??= _clientFactory.CreateClient(_options.Connection);
    }
}

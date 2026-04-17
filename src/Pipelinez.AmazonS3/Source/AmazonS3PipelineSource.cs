using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Pipelinez.AmazonS3.Client;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.AmazonS3.Record;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Source;
using CoreMetadataCollection = Pipelinez.Core.Record.Metadata.MetadataCollection;

namespace Pipelinez.AmazonS3.Source;

/// <summary>
/// Enumerates Amazon S3 objects and publishes mapped records into a Pipelinez pipeline.
/// </summary>
/// <typeparam name="T">The pipeline record type.</typeparam>
public sealed class AmazonS3PipelineSource<T> : PipelineSourceBase<T>
    where T : PipelineRecord
{
    private static readonly TimeSpan CompletionPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly AmazonS3SourceOptions _options;
    private readonly Func<AmazonS3ObjectContext, CancellationToken, Task<T>> _recordMapper;
    private readonly IAmazonS3ClientFactory _clientFactory;
    private readonly ILogger<AmazonS3PipelineSource<T>> _logger;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, AmazonS3PendingObject> _pendingObjects = new(StringComparer.Ordinal);
    private string? _leaseId;

    /// <summary>
    /// Initializes a new Amazon S3 source.
    /// </summary>
    /// <param name="options">The source options.</param>
    /// <param name="recordMapper">Maps an S3 object context into a pipeline record.</param>
    public AmazonS3PipelineSource(
        AmazonS3SourceOptions options,
        Func<AmazonS3ObjectContext, CancellationToken, Task<T>> recordMapper)
        : this(options, recordMapper, AmazonS3ClientFactory.Instance)
    {
    }

    internal AmazonS3PipelineSource(
        AmazonS3SourceOptions options,
        Func<AmazonS3ObjectContext, CancellationToken, Task<T>> recordMapper,
        IAmazonS3ClientFactory clientFactory)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        _recordMapper = recordMapper ?? throw new ArgumentNullException(nameof(recordMapper));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = LoggingManager.Instance.CreateLogger<AmazonS3PipelineSource<T>>();
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        _leaseId = AmazonS3MetadataBuilder.BuildLeaseId(_options);
        _logger.LogInformation(
            "Initializing Amazon S3 source for bucket {BucketName} and prefix {Prefix}.",
            _options.Bucket.BucketName,
            _options.Bucket.Prefix);
    }

    /// <inheritdoc />
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        try
        {
            await using var client = _clientFactory.CreateClient(_options.Connection);

            do
            {
                await EnumerateCurrentListingAsync(client, cancellationToken.Token).ConfigureAwait(false);

                if (!_options.Polling.Enabled || _options.Polling.StopWhenCurrentListingIsComplete)
                {
                    Complete();
                    break;
                }

                await Task.Delay(_options.Polling.Interval, cancellationToken.Token).ConfigureAwait(false);
            }
            while (!cancellationToken.IsCancellationRequested && !Completion.IsCompleted);

            while (!cancellationToken.IsCancellationRequested && !Completion.IsCompleted)
            {
                await Task.Delay(CompletionPollInterval, cancellationToken.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            CancelPendingObjects();
        }
    }

    /// <inheritdoc />
    public override void OnPipelineContainerComplete(
        object sender,
        PipelineContainerCompletedEventHandlerArgs<PipelineContainer<T>> e)
    {
        var settlementKey = TryGetSettlementKey(e.Container.Metadata);
        if (settlementKey is null)
        {
            return;
        }

        ResolvePendingObject(settlementKey, _options.Settlement.GetSuccessDecision());
    }

    internal override void OnPipelineContainerFaultHandled(
        object sender,
        PipelineContainerFaultHandledEventHandlerArgs<PipelineContainer<T>> e)
    {
        var settlementKey = TryGetSettlementKey(e.Container.Metadata);
        if (settlementKey is null)
        {
            return;
        }

        var decision = e.Action switch
        {
            PipelineErrorAction.SkipRecord => _options.Settlement.GetSkippedDecision(),
            PipelineErrorAction.DeadLetter => _options.Settlement.GetDeadLetterDecision(),
            _ => AmazonS3ObjectSettlementDecision.Leave
        };

        ResolvePendingObject(settlementKey, decision);
    }

    private async Task EnumerateCurrentListingAsync(
        IAmazonS3Client client,
        CancellationToken cancellationToken)
    {
        string? continuationToken = null;

        do
        {
            var response = await client
                .ListObjectsV2Async(
                    new ListObjectsV2Request
                    {
                        BucketName = _options.Bucket.BucketName,
                        Prefix = _options.Bucket.Prefix,
                        ContinuationToken = continuationToken,
                        MaxKeys = _options.Polling.MaxKeysPerRequest
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var s3Object in (response.S3Objects ?? new List<S3Object>()).Where(_options.Filter.Allows))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessObjectAsync(client, s3Object, cancellationToken).ConfigureAwait(false);
            }

            continuationToken = response.IsTruncated == true
                ? response.NextContinuationToken
                : null;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));
    }

    private async Task ProcessObjectAsync(
        IAmazonS3Client client,
        S3Object s3Object,
        CancellationToken cancellationToken)
    {
        var settlementKey = Guid.NewGuid().ToString("N");
        AmazonS3PendingObject? pendingObject = null;

        try
        {
            using var response = await client
                .GetObjectAsync(
                    new GetObjectRequest
                    {
                        BucketName = _options.Bucket.BucketName,
                        Key = s3Object.Key
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var tags = await LoadTagsAsync(client, s3Object, cancellationToken).ConfigureAwait(false);
            pendingObject = new AmazonS3PendingObject(settlementKey, s3Object.Key, response.VersionId);
            AddPendingObject(pendingObject);

            var context = CreateContext(s3Object, response, tags);
            var record = await _recordMapper(context, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException("Amazon S3 record mapper returned null.");

            var metadata = AmazonS3MetadataBuilder.CreateMetadata(
                _options,
                s3Object,
                response,
                settlementKey,
                _leaseId ?? AmazonS3MetadataBuilder.BuildLeaseId(_options));

            await PublishAsync(record, metadata).ConfigureAwait(false);

            var decision = await pendingObject.Settlement.ConfigureAwait(false);
            await SettleObjectAsync(client, pendingObject, decision, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            RemovePendingObject(settlementKey);
            throw;
        }
        catch (Exception exception)
        {
            RemovePendingObject(settlementKey);
            _logger.LogError(
                exception,
                "Error processing Amazon S3 object {ObjectKey} from bucket {BucketName}.",
                s3Object.Key,
                _options.Bucket.BucketName);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTagsAsync(
        IAmazonS3Client client,
        S3Object s3Object,
        CancellationToken cancellationToken)
    {
        if (!_options.LoadObjectTags)
        {
            return new Dictionary<string, string>();
        }

        var response = await client
            .GetObjectTaggingAsync(
                new GetObjectTaggingRequest
                {
                    BucketName = _options.Bucket.BucketName,
                    Key = s3Object.Key
                },
                cancellationToken)
            .ConfigureAwait(false);

        return AmazonS3Tags.ToDictionary(response.Tagging);
    }

    private AmazonS3ObjectContext CreateContext(
        S3Object s3Object,
        GetObjectResponse response,
        IReadOnlyDictionary<string, string> tags)
    {
        var metadata = response.Metadata.Keys
            .ToDictionary(key => key, key => response.Metadata[key], StringComparer.OrdinalIgnoreCase);

        return new AmazonS3ObjectContext
        {
            BucketName = _options.Bucket.BucketName,
            Key = s3Object.Key,
            VersionId = response.VersionId,
            ETag = s3Object.ETag,
            SizeBytes = s3Object.Size.GetValueOrDefault(),
            LastModifiedUtc = s3Object.LastModified ?? DateTime.MinValue,
            ContentType = response.Headers.ContentType,
            Content = response.ResponseStream,
            Metadata = metadata,
            Tags = tags
        };
    }

    private void AddPendingObject(AmazonS3PendingObject pendingObject)
    {
        lock (_stateLock)
        {
            _pendingObjects[pendingObject.SettlementKey] = pendingObject;
        }
    }

    private void ResolvePendingObject(
        string settlementKey,
        AmazonS3ObjectSettlementDecision decision)
    {
        AmazonS3PendingObject? pendingObject;

        lock (_stateLock)
        {
            if (!_pendingObjects.Remove(settlementKey, out pendingObject))
            {
                return;
            }
        }

        pendingObject.TrySetSettlement(decision);
    }

    private void RemovePendingObject(string settlementKey)
    {
        AmazonS3PendingObject? pendingObject;

        lock (_stateLock)
        {
            if (!_pendingObjects.Remove(settlementKey, out pendingObject))
            {
                return;
            }
        }

        pendingObject.TryCancel();
    }

    private void CancelPendingObjects()
    {
        AmazonS3PendingObject[] pendingObjects;

        lock (_stateLock)
        {
            pendingObjects = _pendingObjects.Values.ToArray();
            _pendingObjects.Clear();
        }

        foreach (var pendingObject in pendingObjects)
        {
            pendingObject.TryCancel();
        }
    }

    private async Task SettleObjectAsync(
        IAmazonS3Client client,
        AmazonS3PendingObject pendingObject,
        AmazonS3ObjectSettlementDecision decision,
        CancellationToken cancellationToken)
    {
        switch (decision.Action)
        {
            case AmazonS3ObjectSettlementAction.Leave:
                return;
            case AmazonS3ObjectSettlementAction.Delete:
                await DeleteObjectAsync(client, pendingObject, cancellationToken).ConfigureAwait(false);
                return;
            case AmazonS3ObjectSettlementAction.Tag:
                await TagObjectAsync(client, pendingObject, decision.Tags, cancellationToken).ConfigureAwait(false);
                return;
            case AmazonS3ObjectSettlementAction.Copy:
                await CopyObjectAsync(client, pendingObject, decision, cancellationToken).ConfigureAwait(false);
                return;
            case AmazonS3ObjectSettlementAction.Move:
                await CopyObjectAsync(client, pendingObject, decision, cancellationToken).ConfigureAwait(false);
                await DeleteObjectAsync(client, pendingObject, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.Action, "Unknown Amazon S3 settlement action.");
        }
    }

    private Task DeleteObjectAsync(
        IAmazonS3Client client,
        AmazonS3PendingObject pendingObject,
        CancellationToken cancellationToken)
    {
        return client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _options.Bucket.BucketName,
                Key = pendingObject.ObjectKey,
                VersionId = pendingObject.VersionId
            },
            cancellationToken);
    }

    private Task TagObjectAsync(
        IAmazonS3Client client,
        AmazonS3PendingObject pendingObject,
        IReadOnlyDictionary<string, string> tags,
        CancellationToken cancellationToken)
    {
        return client.PutObjectTaggingAsync(
            new PutObjectTaggingRequest
            {
                BucketName = _options.Bucket.BucketName,
                Key = pendingObject.ObjectKey,
                VersionId = pendingObject.VersionId,
                Tagging = new Tagging
                {
                    TagSet = AmazonS3Tags.Merge(tags, new Dictionary<string, string>())
                }
            },
            cancellationToken);
    }

    private Task CopyObjectAsync(
        IAmazonS3Client client,
        AmazonS3PendingObject pendingObject,
        AmazonS3ObjectSettlementDecision decision,
        CancellationToken cancellationToken)
    {
        var targetPrefix = decision.TargetPrefix
                           ?? throw new InvalidOperationException("Amazon S3 copy or move settlement requires a target prefix.");
        var targetKey = AmazonS3ObjectKey.BuildTargetKey(
            _options.Bucket.Prefix,
            pendingObject.ObjectKey,
            targetPrefix);

        return client.CopyObjectAsync(
            new CopyObjectRequest
            {
                SourceBucket = _options.Bucket.BucketName,
                SourceKey = pendingObject.ObjectKey,
                SourceVersionId = pendingObject.VersionId,
                DestinationBucket = _options.Bucket.BucketName,
                DestinationKey = targetKey
            },
            cancellationToken);
    }

    private static string? TryGetSettlementKey(CoreMetadataCollection metadata)
    {
        return metadata.GetValue(AmazonS3MetadataKeys.SettlementKey);
    }
}

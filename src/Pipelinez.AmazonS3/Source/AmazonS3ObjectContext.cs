namespace Pipelinez.AmazonS3.Source;

/// <summary>
/// Provides the source object payload and S3 metadata to a record mapper.
/// </summary>
public sealed class AmazonS3ObjectContext
{
    /// <summary>
    /// Gets the bucket that contains the source object.
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// Gets the source object key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the source object version identifier when available.
    /// </summary>
    public string? VersionId { get; init; }

    /// <summary>
    /// Gets the source object ETag when available.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// Gets the object size in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Gets the object last-modified timestamp.
    /// </summary>
    public DateTime LastModifiedUtc { get; init; }

    /// <summary>
    /// Gets the object content type when available.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the object content stream. The stream is valid only during mapper execution.
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>
    /// Gets the user metadata returned by S3 for the object.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the object tags when tag loading is enabled.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}

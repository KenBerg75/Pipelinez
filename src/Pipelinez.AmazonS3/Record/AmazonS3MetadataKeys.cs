namespace Pipelinez.AmazonS3.Record;

/// <summary>
/// Defines metadata keys stamped on records consumed from Amazon S3.
/// </summary>
public static class AmazonS3MetadataKeys
{
    /// <summary>
    /// Metadata key for the S3 bucket name.
    /// </summary>
    public const string BucketName = "pipelinez.amazon_s3.bucket_name";

    /// <summary>
    /// Metadata key for the S3 object key.
    /// </summary>
    public const string ObjectKey = "pipelinez.amazon_s3.object_key";

    /// <summary>
    /// Metadata key for the S3 object version id.
    /// </summary>
    public const string VersionId = "pipelinez.amazon_s3.version_id";

    /// <summary>
    /// Metadata key for the S3 object ETag.
    /// </summary>
    public const string ETag = "pipelinez.amazon_s3.etag";

    /// <summary>
    /// Metadata key for the S3 object size in bytes.
    /// </summary>
    public const string SizeBytes = "pipelinez.amazon_s3.size_bytes";

    /// <summary>
    /// Metadata key for the S3 object last-modified timestamp.
    /// </summary>
    public const string LastModified = "pipelinez.amazon_s3.last_modified";

    /// <summary>
    /// Metadata key for the S3 object content type.
    /// </summary>
    public const string ContentType = "pipelinez.amazon_s3.content_type";

    /// <summary>
    /// Metadata key for the S3 object storage class.
    /// </summary>
    public const string StorageClass = "pipelinez.amazon_s3.storage_class";

    /// <summary>
    /// Metadata key for the S3 source prefix.
    /// </summary>
    public const string SourcePrefix = "pipelinez.amazon_s3.source_prefix";

    /// <summary>
    /// Metadata key for source object settlement tracking.
    /// </summary>
    public const string SettlementKey = "pipelinez.amazon_s3.settlement_key";
}

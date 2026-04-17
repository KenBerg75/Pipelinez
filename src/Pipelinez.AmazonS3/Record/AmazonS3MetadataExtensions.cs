using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.AmazonS3.Record;

/// <summary>
/// Provides helper methods for reading Amazon S3 metadata from records.
/// </summary>
public static class AmazonS3MetadataExtensions
{
    /// <summary>
    /// Gets the Amazon S3 bucket name from metadata.
    /// </summary>
    /// <param name="metadata">The metadata to inspect.</param>
    /// <returns>The bucket name, or <see langword="null" /> when absent.</returns>
    public static string? GetAmazonS3BucketName(this MetadataCollection metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.GetValue(AmazonS3MetadataKeys.BucketName);
    }

    /// <summary>
    /// Gets the Amazon S3 object key from metadata.
    /// </summary>
    /// <param name="metadata">The metadata to inspect.</param>
    /// <returns>The object key, or <see langword="null" /> when absent.</returns>
    public static string? GetAmazonS3ObjectKey(this MetadataCollection metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.GetValue(AmazonS3MetadataKeys.ObjectKey);
    }

    /// <summary>
    /// Gets the Amazon S3 object version id from metadata.
    /// </summary>
    /// <param name="metadata">The metadata to inspect.</param>
    /// <returns>The object version id, or <see langword="null" /> when absent.</returns>
    public static string? GetAmazonS3VersionId(this MetadataCollection metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.GetValue(AmazonS3MetadataKeys.VersionId);
    }

    /// <summary>
    /// Gets the Amazon S3 object ETag from metadata.
    /// </summary>
    /// <param name="metadata">The metadata to inspect.</param>
    /// <returns>The object ETag, or <see langword="null" /> when absent.</returns>
    public static string? GetAmazonS3ETag(this MetadataCollection metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.GetValue(AmazonS3MetadataKeys.ETag);
    }
}

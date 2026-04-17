using System.Globalization;
using Amazon.S3.Model;
using Pipelinez.AmazonS3.Configuration;
using Pipelinez.Core.Distributed;
using CoreMetadataCollection = Pipelinez.Core.Record.Metadata.MetadataCollection;

namespace Pipelinez.AmazonS3.Record;

internal static class AmazonS3MetadataBuilder
{
    public const string TransportName = "AmazonS3";

    public static CoreMetadataCollection CreateMetadata(
        AmazonS3SourceOptions options,
        S3Object s3Object,
        GetObjectResponse response,
        string settlementKey,
        string leaseId)
    {
        var metadata = new CoreMetadataCollection();
        metadata.Set(AmazonS3MetadataKeys.BucketName, options.Bucket.BucketName);
        metadata.Set(AmazonS3MetadataKeys.ObjectKey, s3Object.Key);
        metadata.Set(AmazonS3MetadataKeys.SizeBytes, s3Object.Size.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
        metadata.Set(AmazonS3MetadataKeys.SettlementKey, settlementKey);
        SetIfPresent(metadata, AmazonS3MetadataKeys.SourcePrefix, options.Bucket.Prefix);
        SetIfPresent(metadata, AmazonS3MetadataKeys.VersionId, response.VersionId);
        SetIfPresent(metadata, AmazonS3MetadataKeys.ETag, s3Object.ETag);
        SetIfPresent(metadata, AmazonS3MetadataKeys.ContentType, response.Headers.ContentType);
        SetIfPresent(metadata, AmazonS3MetadataKeys.StorageClass, s3Object.StorageClass);

        if (s3Object.LastModified is not null)
        {
            metadata.Set(
                AmazonS3MetadataKeys.LastModified,
                s3Object.LastModified.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        metadata.Set(DistributedMetadataKeys.TransportName, TransportName);
        metadata.Set(DistributedMetadataKeys.LeaseId, leaseId);
        metadata.Set(DistributedMetadataKeys.PartitionKey, options.Bucket.BucketName);
        metadata.Set(DistributedMetadataKeys.Offset, s3Object.Key);
        return metadata;
    }

    public static string BuildLeaseId(AmazonS3SourceOptions options)
    {
        return $"amazon-s3:{options.Bucket.BucketName}:{options.Bucket.Prefix ?? string.Empty}";
    }

    private static void SetIfPresent(CoreMetadataCollection metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata.Set(key, value);
        }
    }
}

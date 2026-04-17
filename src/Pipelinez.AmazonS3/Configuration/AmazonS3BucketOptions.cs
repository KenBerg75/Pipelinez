namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures an Amazon S3 bucket and optional prefix.
/// </summary>
public sealed class AmazonS3BucketOptions
{
    /// <summary>
    /// Gets the S3 bucket name, access point ARN, or compatible endpoint bucket identifier.
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// Gets the optional object key prefix used for listing.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Validates the bucket options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated bucket options.</returns>
    public AmazonS3BucketOptions Validate()
    {
        if (string.IsNullOrWhiteSpace(BucketName))
        {
            throw new InvalidOperationException("Amazon S3 bucket name is required.");
        }

        if (Prefix is not null && Prefix.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new InvalidOperationException("Amazon S3 prefix cannot contain control characters.");
        }

        return this;
    }
}

namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures default object-write behavior for S3 destinations.
/// </summary>
public sealed class AmazonS3ObjectWriteOptions
{
    /// <summary>
    /// Gets an optional prefix prepended to mapped object keys.
    /// </summary>
    public string? KeyPrefix { get; init; }

    /// <summary>
    /// Gets the default content type for mapped objects.
    /// </summary>
    public string? ContentType { get; init; } = "application/json";

    /// <summary>
    /// Gets default object metadata applied to mapped objects.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets default object tags applied to mapped objects.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Validates the write options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated write options.</returns>
    public AmazonS3ObjectWriteOptions Validate()
    {
        AmazonS3Validation.ValidateOptionalKeyPrefix(KeyPrefix, "Amazon S3 key prefix");
        AmazonS3Validation.ValidateDictionary(Metadata, "Amazon S3 metadata");
        AmazonS3Validation.ValidateDictionary(Tags, "Amazon S3 tags");
        return this;
    }
}

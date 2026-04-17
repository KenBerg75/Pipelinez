using Amazon.Runtime;
using Amazon.S3;

namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures Amazon S3 client creation.
/// </summary>
public sealed class AmazonS3ConnectionOptions
{
    /// <summary>
    /// Gets or sets the AWS region system name, such as <c>us-east-1</c>.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Gets or sets an optional S3-compatible service URL.
    /// </summary>
    public string? ServiceUrl { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether path-style addressing should be used.
    /// </summary>
    public bool ForcePathStyle { get; init; }

    /// <summary>
    /// Gets or sets the AWS shared credentials profile name.
    /// </summary>
    public string? ProfileName { get; init; }

    /// <summary>
    /// Gets or sets explicit AWS credentials. When omitted, the AWS SDK default credential chain is used.
    /// </summary>
    public AWSCredentials? Credentials { get; init; }

    /// <summary>
    /// Gets or sets the AWS SDK request checksum behavior.
    /// </summary>
    public RequestChecksumCalculation? RequestChecksumCalculation { get; init; }

    /// <summary>
    /// Gets or sets an optional callback for final client configuration.
    /// </summary>
    public Action<AmazonS3Config>? ConfigureClient { get; init; }

    /// <summary>
    /// Validates the connection options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated connection options.</returns>
    public AmazonS3ConnectionOptions Validate()
    {
        if (!string.IsNullOrWhiteSpace(ServiceUrl) &&
            !Uri.TryCreate(ServiceUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Amazon S3 service URL must be an absolute URI when provided.");
        }

        if (!string.IsNullOrWhiteSpace(Region) &&
            string.IsNullOrWhiteSpace(Region.Trim()))
        {
            throw new InvalidOperationException("Amazon S3 region cannot be empty when provided.");
        }

        return this;
    }
}

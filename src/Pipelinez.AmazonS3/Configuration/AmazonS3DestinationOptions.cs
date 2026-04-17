namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures Amazon S3 destination writes for normal pipeline records.
/// </summary>
public sealed class AmazonS3DestinationOptions
{
    /// <summary>
    /// Gets the S3 connection options.
    /// </summary>
    public AmazonS3ConnectionOptions Connection { get; init; } = new();

    /// <summary>
    /// Gets the destination bucket name.
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// Gets default object write options.
    /// </summary>
    public AmazonS3ObjectWriteOptions Write { get; init; } = new();

    /// <summary>
    /// Validates the destination options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated destination options.</returns>
    public AmazonS3DestinationOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Write);

        if (string.IsNullOrWhiteSpace(BucketName))
        {
            throw new InvalidOperationException("Amazon S3 destination bucket name is required.");
        }

        Connection.Validate();
        Write.Validate();
        return this;
    }
}

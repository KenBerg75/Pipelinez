namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures an Amazon S3 object enumeration source.
/// </summary>
public sealed class AmazonS3SourceOptions
{
    /// <summary>
    /// Gets the S3 connection options.
    /// </summary>
    public AmazonS3ConnectionOptions Connection { get; init; } = new();

    /// <summary>
    /// Gets the source bucket and prefix.
    /// </summary>
    public required AmazonS3BucketOptions Bucket { get; init; }

    /// <summary>
    /// Gets client-side object filters.
    /// </summary>
    public AmazonS3ObjectFilterOptions Filter { get; init; } = new();

    /// <summary>
    /// Gets source polling options.
    /// </summary>
    public AmazonS3PollingOptions Polling { get; init; } = new();

    /// <summary>
    /// Gets terminal source object settlement options.
    /// </summary>
    public AmazonS3ObjectSettlementOptions Settlement { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether object tags should be loaded into source contexts.
    /// </summary>
    public bool LoadObjectTags { get; init; }

    /// <summary>
    /// Validates the source options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated source options.</returns>
    public AmazonS3SourceOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Bucket);
        ArgumentNullException.ThrowIfNull(Filter);
        ArgumentNullException.ThrowIfNull(Polling);
        ArgumentNullException.ThrowIfNull(Settlement);

        Connection.Validate();
        Bucket.Validate();
        Filter.Validate();
        Polling.Validate();
        Settlement.Validate();

        return this;
    }
}

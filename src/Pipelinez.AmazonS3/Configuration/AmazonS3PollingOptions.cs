namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures S3 source listing and polling behavior.
/// </summary>
public sealed class AmazonS3PollingOptions
{
    /// <summary>
    /// Gets a value indicating whether the source should repeatedly poll the prefix.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the interval between polling passes.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets a value indicating whether one-shot mode should complete after the current listing.
    /// </summary>
    public bool StopWhenCurrentListingIsComplete { get; init; } = true;

    /// <summary>
    /// Gets the maximum keys requested per list operation.
    /// </summary>
    public int MaxKeysPerRequest { get; init; } = 1000;

    /// <summary>
    /// Validates the polling options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated polling options.</returns>
    public AmazonS3PollingOptions Validate()
    {
        if (Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Amazon S3 polling interval must be greater than zero.");
        }

        if (MaxKeysPerRequest is < 1 or > 1000)
        {
            throw new InvalidOperationException("Amazon S3 max keys per request must be between 1 and 1000.");
        }

        return this;
    }
}

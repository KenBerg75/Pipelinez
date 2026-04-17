using Amazon.S3.Model;

namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures client-side filtering for listed S3 objects.
/// </summary>
public sealed class AmazonS3ObjectFilterOptions
{
    /// <summary>
    /// Gets the optional required key suffix.
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Gets the optional minimum object size in bytes.
    /// </summary>
    public long? MinSizeBytes { get; init; }

    /// <summary>
    /// Gets the optional maximum object size in bytes.
    /// </summary>
    public long? MaxSizeBytes { get; init; }

    /// <summary>
    /// Gets the optional lower bound for object last-modified time.
    /// </summary>
    public DateTimeOffset? LastModifiedAfter { get; init; }

    /// <summary>
    /// Gets an optional custom predicate for listed objects.
    /// </summary>
    public Func<S3Object, bool>? Predicate { get; init; }

    /// <summary>
    /// Validates the filter options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated filter options.</returns>
    public AmazonS3ObjectFilterOptions Validate()
    {
        if (MinSizeBytes is < 0)
        {
            throw new InvalidOperationException("Amazon S3 minimum object size cannot be negative.");
        }

        if (MaxSizeBytes is < 0)
        {
            throw new InvalidOperationException("Amazon S3 maximum object size cannot be negative.");
        }

        if (MinSizeBytes.HasValue && MaxSizeBytes.HasValue && MinSizeBytes > MaxSizeBytes)
        {
            throw new InvalidOperationException("Amazon S3 minimum object size cannot exceed maximum object size.");
        }

        return this;
    }

    internal bool Allows(S3Object s3Object)
    {
        ArgumentNullException.ThrowIfNull(s3Object);

        if (!string.IsNullOrEmpty(Suffix) &&
            !s3Object.Key.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (MinSizeBytes.HasValue && s3Object.Size < MinSizeBytes.Value)
        {
            return false;
        }

        if (MaxSizeBytes.HasValue && s3Object.Size > MaxSizeBytes.Value)
        {
            return false;
        }

        if (LastModifiedAfter.HasValue && s3Object.LastModified <= LastModifiedAfter.Value)
        {
            return false;
        }

        return Predicate?.Invoke(s3Object) ?? true;
    }
}

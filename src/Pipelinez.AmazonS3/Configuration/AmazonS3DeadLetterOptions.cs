using System.Text.Json;

namespace Pipelinez.AmazonS3.Configuration;

/// <summary>
/// Configures Amazon S3 writes for Pipelinez dead-letter records.
/// </summary>
public sealed class AmazonS3DeadLetterOptions
{
    /// <summary>
    /// Gets the S3 connection options.
    /// </summary>
    public AmazonS3ConnectionOptions Connection { get; init; } = new();

    /// <summary>
    /// Gets the dead-letter artifact bucket name.
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// Gets the key prefix used by default dead-letter object mapping.
    /// </summary>
    public string KeyPrefix { get; init; } = "dead-letter/";

    /// <summary>
    /// Gets default object write options.
    /// </summary>
    public AmazonS3ObjectWriteOptions Write { get; init; } = new();

    /// <summary>
    /// Gets JSON serializer options used by default dead-letter object mapping.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; init; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Validates the dead-letter options and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated dead-letter options.</returns>
    public AmazonS3DeadLetterOptions Validate()
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentNullException.ThrowIfNull(Write);
        ArgumentNullException.ThrowIfNull(SerializerOptions);

        if (string.IsNullOrWhiteSpace(BucketName))
        {
            throw new InvalidOperationException("Amazon S3 dead-letter bucket name is required.");
        }

        AmazonS3Validation.ValidateOptionalKeyPrefix(KeyPrefix, "Amazon S3 dead-letter key prefix");
        Connection.Validate();
        Write.Validate();
        return this;
    }
}

using System.Text;
using Amazon.S3.Model;
using Pipelinez.AmazonS3.Configuration;

namespace Pipelinez.AmazonS3.Destination;

/// <summary>
/// Describes an object to write to Amazon S3.
/// </summary>
public sealed class AmazonS3PutObject
{
    /// <summary>
    /// Gets the destination object key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the text payload to write.
    /// </summary>
    public string? TextContent { get; init; }

    /// <summary>
    /// Gets the binary payload to write.
    /// </summary>
    public byte[]? BinaryContent { get; init; }

    /// <summary>
    /// Gets the stream payload to write.
    /// </summary>
    public Stream? Content { get; init; }

    /// <summary>
    /// Gets the content type for the object.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets custom S3 object metadata to include on the object.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets tags to apply to the object.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates a text object description.
    /// </summary>
    /// <param name="key">The destination object key.</param>
    /// <param name="content">The text payload.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>The object description.</returns>
    public static AmazonS3PutObject FromText(
        string key,
        string content,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new AmazonS3PutObject
        {
            Key = key,
            TextContent = content,
            ContentType = contentType
        };
    }

    /// <summary>
    /// Creates a binary object description.
    /// </summary>
    /// <param name="key">The destination object key.</param>
    /// <param name="content">The binary payload.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>The object description.</returns>
    public static AmazonS3PutObject FromBytes(
        string key,
        byte[] content,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new AmazonS3PutObject
        {
            Key = key,
            BinaryContent = content,
            ContentType = contentType
        };
    }

    /// <summary>
    /// Creates a stream object description.
    /// </summary>
    /// <param name="key">The destination object key.</param>
    /// <param name="content">The stream payload.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>The object description.</returns>
    public static AmazonS3PutObject FromStream(
        string key,
        Stream content,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new AmazonS3PutObject
        {
            Key = key,
            Content = content,
            ContentType = contentType
        };
    }

    /// <summary>
    /// Validates the object description and returns the same instance when valid.
    /// </summary>
    /// <returns>The validated object description.</returns>
    public AmazonS3PutObject Validate()
    {
        AmazonS3Validation.ValidateObjectKey(Key, nameof(Key));
        AmazonS3Validation.ValidateDictionary(Metadata, nameof(Metadata));
        AmazonS3Validation.ValidateDictionary(Tags, nameof(Tags));

        var contentSourceCount = 0;
        if (TextContent is not null)
        {
            contentSourceCount++;
        }

        if (BinaryContent is not null)
        {
            contentSourceCount++;
        }

        if (Content is not null)
        {
            contentSourceCount++;
        }

        if (contentSourceCount != 1)
        {
            throw new InvalidOperationException("Amazon S3 object publishing requires exactly one content source.");
        }

        return this;
    }

    internal PutObjectRequest BuildRequest(
        string bucketName,
        AmazonS3ObjectWriteOptions defaults)
    {
        Validate();

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = AmazonS3ObjectKey.Build(defaults.KeyPrefix, Key),
            ContentType = ContentType ?? defaults.ContentType
        };

        if (TextContent is not null)
        {
            request.ContentBody = TextContent;
            request.Headers.ContentEncoding = Encoding.UTF8.WebName;
        }
        else if (BinaryContent is not null)
        {
            request.InputStream = new MemoryStream(BinaryContent, writable: false);
        }
        else
        {
            request.InputStream = Content;
        }

        foreach (var metadata in defaults.Metadata)
        {
            request.Metadata[metadata.Key] = metadata.Value;
        }

        foreach (var metadata in Metadata)
        {
            request.Metadata[metadata.Key] = metadata.Value;
        }

        var tags = AmazonS3Tags.Merge(defaults.Tags, Tags);
        if (tags.Count > 0)
        {
            request.TagSet = tags;
        }

        return request;
    }
}

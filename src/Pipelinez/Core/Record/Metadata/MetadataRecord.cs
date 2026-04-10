namespace Pipelinez.Core.Record.Metadata;

/// <summary>
/// Represents a metadata key/value pair associated with a pipeline container.
/// </summary>
public class MetadataRecord
{
    /// <summary>
    /// Gets or sets the metadata key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the metadata value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Initializes a new metadata record.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public MetadataRecord(string key, string value)
    {
        Key = key;
        Value = value;
    }
}

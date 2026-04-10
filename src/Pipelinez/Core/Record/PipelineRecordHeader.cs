namespace Pipelinez.Core.Record;

/// <summary>
/// Represents a string key/value header attached to a <see cref="PipelineRecord" />.
/// </summary>
public class PipelineRecordHeader
{
    /// <summary>
    /// Gets or sets the header key.
    /// </summary>
    public string Key { get; set; } = String.Empty;

    /// <summary>
    /// Gets or sets the header value.
    /// </summary>
    public string Value { get; set; } = String.Empty;
}

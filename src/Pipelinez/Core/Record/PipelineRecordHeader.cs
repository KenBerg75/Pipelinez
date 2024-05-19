namespace Pipelinez.Core.Record;

/// <summary>
/// A string key/value pair of data that represents a header record part of a PipelineRecord 
/// </summary>
public class PipelineRecordHeader
{
    public string Key { get; set; } = String.Empty;
    public string Value { get; set; } = String.Empty;
}
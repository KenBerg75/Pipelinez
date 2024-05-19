namespace Pipelinez.Core.Record.Metadata;

public class MetadataRecord
{
    public string Key { get; set; }
    public string Value { get; set; }

    public MetadataRecord(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
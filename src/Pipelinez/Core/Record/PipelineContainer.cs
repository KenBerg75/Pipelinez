using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Record;

public sealed class PipelineContainer<T> where T : PipelineRecord
{
    /// <summary>
    /// Contains metadata relating to the record.
    /// </summary>
    public MetadataCollection Metadata { get; } = new MetadataCollection();
    
    public T Record { get; set; }
}
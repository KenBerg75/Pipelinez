namespace Pipelinez.Core.Record;



/// <summary>
/// Base class for records that flow through a Pipelinez pipeline.
/// </summary>
public abstract class PipelineRecord
{
    /// <summary>
    /// Gets the headers associated with the record.
    /// </summary>
    public IList<PipelineRecordHeader> Headers { get; } = new List<PipelineRecordHeader>();
}

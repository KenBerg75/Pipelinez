namespace Pipelinez.Core.Record;



/// <summary>
/// Base class for any data structure that needs to flow through a pipeline
/// </summary>
public abstract class PipelineRecord
{
    /// <summary>
    /// Headers associated to the record
    /// </summary>
    public IList<PipelineRecordHeader> Headers { get; } = new List<PipelineRecordHeader>();
}
using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record.Metadata;

namespace Pipelinez.Core.Record;

public sealed class PipelineContainer<T> where T : PipelineRecord
{
    public PipelineContainer(T record) : this(record, new MetadataCollection())
    {
    }

    public PipelineContainer(T record, MetadataCollection metadata)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Metadata = Guard.Against.Null(metadata, nameof(metadata));
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// Contains metadata relating to the record.
    /// </summary>
    public MetadataCollection Metadata { get; }

    public DateTimeOffset CreatedAtUtc { get; }
    
    public T Record { get; set; }

    public PipelineFaultState? Fault { get; private set; }

    public bool HasFault => Fault is not null;

    public IList<PipelineSegmentExecution> SegmentHistory { get; } = new List<PipelineSegmentExecution>();

    public void MarkFaulted(PipelineFaultState fault)
    {
        Fault ??= Guard.Against.Null(fault, nameof(fault));
    }

    public void AddSegmentExecution(PipelineSegmentExecution segmentExecution)
    {
        SegmentHistory.Add(Guard.Against.Null(segmentExecution, nameof(segmentExecution)));
    }
}

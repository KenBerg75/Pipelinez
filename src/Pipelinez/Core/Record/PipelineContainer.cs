using Ardalis.GuardClauses;
using Pipelinez.Core.FaultHandling;
using Pipelinez.Core.Record.Metadata;
using Pipelinez.Core.Retry;

namespace Pipelinez.Core.Record;

/// <summary>
/// Wraps a pipeline record with metadata, fault state, and execution history while it moves through the pipeline.
/// </summary>
/// <typeparam name="T">The pipeline record type carried by the container.</typeparam>
public sealed class PipelineContainer<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new container for the supplied record with an empty metadata collection.
    /// </summary>
    /// <param name="record">The record to wrap.</param>
    public PipelineContainer(T record) : this(record, new MetadataCollection())
    {
    }

    /// <summary>
    /// Initializes a new container for the supplied record and metadata.
    /// </summary>
    /// <param name="record">The record to wrap.</param>
    /// <param name="metadata">The metadata associated with the record.</param>
    public PipelineContainer(T record, MetadataCollection metadata)
    {
        Record = Guard.Against.Null(record, nameof(record));
        Metadata = Guard.Against.Null(metadata, nameof(metadata));
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// Gets the metadata associated with the record.
    /// </summary>
    public MetadataCollection Metadata { get; }

    /// <summary>
    /// Gets the time the container was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets or sets the record carried by the container.
    /// </summary>
    public T Record { get; set; }

    /// <summary>
    /// Gets the fault state recorded for the container, if any.
    /// </summary>
    public PipelineFaultState? Fault { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the container has faulted.
    /// </summary>
    public bool HasFault => Fault is not null;

    /// <summary>
    /// Gets the ordered execution history recorded for pipeline segments.
    /// </summary>
    public IList<PipelineSegmentExecution> SegmentHistory { get; } = new List<PipelineSegmentExecution>();

    /// <summary>
    /// Gets the ordered retry history recorded for the container.
    /// </summary>
    public IList<PipelineRetryAttempt> RetryHistory { get; } = new List<PipelineRetryAttempt>();

    /// <summary>
    /// Marks the container as faulted.
    /// </summary>
    /// <param name="fault">The fault state to record.</param>
    public void MarkFaulted(PipelineFaultState fault)
    {
        Fault ??= Guard.Against.Null(fault, nameof(fault));
    }

    /// <summary>
    /// Adds a segment execution entry to the container history.
    /// </summary>
    /// <param name="segmentExecution">The execution record to add.</param>
    public void AddSegmentExecution(PipelineSegmentExecution segmentExecution)
    {
        SegmentHistory.Add(Guard.Against.Null(segmentExecution, nameof(segmentExecution)));
    }

    /// <summary>
    /// Adds a retry attempt entry to the container history.
    /// </summary>
    /// <param name="retryAttempt">The retry attempt to add.</param>
    public void AddRetryAttempt(PipelineRetryAttempt retryAttempt)
    {
        RetryHistory.Add(Guard.Against.Null(retryAttempt, nameof(retryAttempt)));
    }
}

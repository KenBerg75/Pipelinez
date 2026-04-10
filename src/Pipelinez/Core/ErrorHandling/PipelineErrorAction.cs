namespace Pipelinez.Core.ErrorHandling;

/// <summary>
/// Describes the terminal action a pipeline should take after retries are exhausted for a faulted record.
/// </summary>
public enum PipelineErrorAction
{
    /// <summary>
    /// Fault the pipeline and stop processing.
    /// </summary>
    StopPipeline,
    /// <summary>
    /// Skip the faulted record and continue processing later records.
    /// </summary>
    SkipRecord,
    /// <summary>
    /// Rethrow the fault and fault the pipeline.
    /// </summary>
    Rethrow,
    /// <summary>
    /// Dead-letter the record and continue processing when dead-lettering succeeds.
    /// </summary>
    DeadLetter
}

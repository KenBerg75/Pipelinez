using Pipelinez.Core.Record;

namespace Pipelinez.Core.DeadLettering;

/// <summary>
/// Defines a destination that can persist dead-letter records after terminal fault handling.
/// </summary>
/// <typeparam name="T">The pipeline record type contained in the dead-letter envelope.</typeparam>
public interface IPipelineDeadLetterDestination<T> where T : PipelineRecord
{
    /// <summary>
    /// Writes a dead-letter record to the configured store.
    /// </summary>
    /// <param name="deadLetterRecord">The dead-letter record to persist.</param>
    /// <param name="cancellationToken">A token used to cancel the write operation.</param>
    Task WriteAsync(PipelineDeadLetterRecord<T> deadLetterRecord, CancellationToken cancellationToken);
}

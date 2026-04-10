using System.Collections.Concurrent;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.DeadLettering;

/// <summary>
/// Stores dead-letter records in memory for testing or simple host scenarios.
/// </summary>
/// <typeparam name="T">The pipeline record type stored in the dead-letter envelopes.</typeparam>
public sealed class InMemoryDeadLetterDestination<T> : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
{
    private readonly ConcurrentQueue<PipelineDeadLetterRecord<T>> _records = new();

    /// <summary>
    /// Gets the dead-letter records written to the in-memory destination.
    /// </summary>
    public IReadOnlyList<PipelineDeadLetterRecord<T>> Records => _records.ToArray();

    /// <inheritdoc />
    public Task WriteAsync(PipelineDeadLetterRecord<T> deadLetterRecord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterRecord);
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(deadLetterRecord);
        return Task.CompletedTask;
    }
}

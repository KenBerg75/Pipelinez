using System.Collections.Concurrent;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.DeadLettering;

public sealed class InMemoryDeadLetterDestination<T> : IPipelineDeadLetterDestination<T>
    where T : PipelineRecord
{
    private readonly ConcurrentQueue<PipelineDeadLetterRecord<T>> _records = new();

    public IReadOnlyList<PipelineDeadLetterRecord<T>> Records => _records.ToArray();

    public Task WriteAsync(PipelineDeadLetterRecord<T> deadLetterRecord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterRecord);
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(deadLetterRecord);
        return Task.CompletedTask;
    }
}

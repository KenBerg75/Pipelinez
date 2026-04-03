using System.Collections.Concurrent;
using Pipelinez.Core.Destination;

namespace Pipelinez.Kafka.Tests.Models;

public sealed class BlockingCountingDestination : PipelineDestination<TestKafkaRecord>
{
    private readonly TaskCompletionSource _allowFirstExecution =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _executionCount;

    public TaskCompletionSource FirstExecutionStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentQueue<string> ProcessedKeys { get; } = new();

    protected override async Task ExecuteAsync(TestKafkaRecord record, CancellationToken cancellationToken)
    {
        var executionNumber = Interlocked.Increment(ref _executionCount);
        if (executionNumber == 1)
        {
            FirstExecutionStarted.TrySetResult();
            await _allowFirstExecution.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        ProcessedKeys.Enqueue(record.Key);
    }

    public void ReleaseFirstExecution()
    {
        _allowFirstExecution.TrySetResult();
    }

    protected override void Initialize()
    {
    }
}

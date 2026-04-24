using Pipelinez.Core;
using Pipelinez.Core.Eventing;

namespace Pipelinez.Benchmarks;

internal sealed class BenchmarkCompletionTracker : IDisposable
{
    private readonly PipelineRecordCompletedEventHandler<BenchmarkRecord> _completedHandler;
    private readonly PipelineRecordDeadLetteredEventHandler<BenchmarkRecord> _deadLetteredHandler;
    private readonly PipelineFaultedEventHandler _faultedHandler;
    private IPipeline<BenchmarkRecord>? _pipeline;
    private Exception? _pipelineFault;
    private int _completedCount;
    private int _deadLetteredCount;

    public BenchmarkCompletionTracker()
    {
        _completedHandler = (_, _) => Interlocked.Increment(ref _completedCount);
        _deadLetteredHandler = (_, _) => Interlocked.Increment(ref _deadLetteredCount);
        _faultedHandler = (_, args) => _pipelineFault = args.Exception;
    }

    public int CompletedCount => Volatile.Read(ref _completedCount);

    public int DeadLetteredCount => Volatile.Read(ref _deadLetteredCount);

    public void Attach(IPipeline<BenchmarkRecord> pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        _pipeline = pipeline;
        pipeline.OnPipelineRecordCompleted += _completedHandler;
        pipeline.OnPipelineRecordDeadLettered += _deadLetteredHandler;
        pipeline.OnPipelineFaulted += _faultedHandler;
    }

    public Task WaitForCompletedAsync(int expectedCount, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return BenchmarkPolling.WaitUntilAsync(
            () =>
            {
                ThrowIfFaulted();
                return CompletedCount >= expectedCount;
            },
            timeout,
            $"Timed out waiting for {expectedCount} completed benchmark records.",
            cancellationToken);
    }

    public Task WaitForDeadLetteredAsync(int expectedCount, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return BenchmarkPolling.WaitUntilAsync(
            () =>
            {
                ThrowIfFaulted();
                return DeadLetteredCount >= expectedCount;
            },
            timeout,
            $"Timed out waiting for {expectedCount} dead-lettered benchmark records.",
            cancellationToken);
    }

    public void Dispose()
    {
        if (_pipeline is null)
        {
            return;
        }

        _pipeline.OnPipelineRecordCompleted -= _completedHandler;
        _pipeline.OnPipelineRecordDeadLettered -= _deadLetteredHandler;
        _pipeline.OnPipelineFaulted -= _faultedHandler;
        _pipeline = null;
    }

    private void ThrowIfFaulted()
    {
        if (_pipelineFault is not null)
        {
            throw new InvalidOperationException("The benchmark pipeline faulted.", _pipelineFault);
        }
    }
}

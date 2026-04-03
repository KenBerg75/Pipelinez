using System.Collections.Concurrent;
using Pipelinez.Core.Segment;
using Pipelinez.Tests.Core.SourceDestTests.Models;

namespace Pipelinez.Tests.Core.PerformanceTests.Models;

public sealed class ParallelTrackingSegment : PipelineSegment<TestPipelineRecord>
{
    private int _currentConcurrency;
    private int _maxConcurrency;

    public int MaxConcurrency => _maxConcurrency;

    public ConcurrentQueue<string> ProcessedValues { get; } = new();

    public override async Task<TestPipelineRecord> ExecuteAsync(TestPipelineRecord arg)
    {
        var current = Interlocked.Increment(ref _currentConcurrency);
        UpdateMaxConcurrency(current);

        try
        {
            await Task.Delay(75).ConfigureAwait(false);
            ProcessedValues.Enqueue(arg.Data);
            return arg;
        }
        finally
        {
            Interlocked.Decrement(ref _currentConcurrency);
        }
    }

    private void UpdateMaxConcurrency(int current)
    {
        while (true)
        {
            var observed = _maxConcurrency;
            if (current <= observed)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maxConcurrency, current, observed) == observed)
            {
                return;
            }
        }
    }
}

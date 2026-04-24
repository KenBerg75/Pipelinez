using Pipelinez.Core.Segment;

namespace Pipelinez.Benchmarks;

internal sealed class FaultingBenchmarkSegment : PipelineSegment<BenchmarkRecord>
{
    public override Task<BenchmarkRecord> ExecuteAsync(BenchmarkRecord arg)
    {
        if (arg.ShouldFault)
        {
            throw new InvalidOperationException($"Benchmark record {arg.Id} is configured to fail.");
        }

        return Task.FromResult(arg);
    }
}

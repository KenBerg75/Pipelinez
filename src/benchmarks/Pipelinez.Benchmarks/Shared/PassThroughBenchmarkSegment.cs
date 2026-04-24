using Pipelinez.Core.Segment;

namespace Pipelinez.Benchmarks;

internal sealed class PassThroughBenchmarkSegment : PipelineSegment<BenchmarkRecord>
{
    public override Task<BenchmarkRecord> ExecuteAsync(BenchmarkRecord arg)
    {
        return Task.FromResult(arg);
    }
}

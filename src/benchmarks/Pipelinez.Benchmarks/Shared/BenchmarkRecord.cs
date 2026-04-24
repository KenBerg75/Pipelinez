using Pipelinez.Core.Record;

namespace Pipelinez.Benchmarks;

public sealed class BenchmarkRecord : PipelineRecord
{
    public required string Id { get; init; }

    public required string Payload { get; init; }

    public bool ShouldFault { get; init; }
}

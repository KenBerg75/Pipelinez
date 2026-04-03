using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class InMemoryPipelineBenchmarks
{
    [Params(1_000)]
    public int RecordCount { get; set; }

    [Benchmark(Baseline = true)]
    public async Task DefaultPipeline()
    {
        var pipeline = CreatePipeline();
        await RunPipelineAsync(pipeline).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task TunedParallelPipeline()
    {
        var pipeline = CreatePipeline(new PipelinePerformanceOptions
        {
            DefaultSegmentExecution = new PipelineExecutionOptions
            {
                BoundedCapacity = 10_000,
                DegreeOfParallelism = Environment.ProcessorCount,
                EnsureOrdered = false
            }
        });

        await RunPipelineAsync(pipeline).ConfigureAwait(false);
    }

    private IPipeline<BenchmarkRecord> CreatePipeline(PipelinePerformanceOptions? performanceOptions = null)
    {
        var builder = Pipeline<BenchmarkRecord>.New("benchmark")
            .WithInMemorySource(new object())
            .AddSegment(new BenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark");

        if (performanceOptions is not null)
        {
            builder.UsePerformanceOptions(performanceOptions);
        }

        return builder.Build();
    }

    private async Task RunPipelineAsync(IPipeline<BenchmarkRecord> pipeline)
    {
        await pipeline.StartPipelineAsync().ConfigureAwait(false);

        for (var i = 0; i < RecordCount; i++)
        {
            await pipeline.PublishAsync(new BenchmarkRecord { Value = i }).ConfigureAwait(false);
        }

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);
    }

    private sealed class BenchmarkRecord : PipelineRecord
    {
        public required int Value { get; init; }
    }

    private sealed class BenchmarkSegment : PipelineSegment<BenchmarkRecord>
    {
        public override Task<BenchmarkRecord> ExecuteAsync(BenchmarkRecord arg)
        {
            return Task.FromResult(arg);
        }
    }
}

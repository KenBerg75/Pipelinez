using BenchmarkDotNet.Attributes;
using Pipelinez.Core;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Performance;

namespace Pipelinez.Benchmarks;

[MemoryDiagnoser]
public class InMemoryPipelineBenchmarks : BenchmarkSuiteBase
{
    private IReadOnlyList<BenchmarkRecord> _successfulRecords = [];
    private IReadOnlyList<BenchmarkRecord> _faultingRecords = [];

    [IterationSetup]
    public void IterationSetup()
    {
        _successfulRecords = CreateSuccessfulRecords();
        _faultingRecords = CreateFaultingRecords();
    }

    [Benchmark(Baseline = true)]
    public Task DefaultPipeline()
    {
        return RunSuccessfulPipelineAsync(performanceOptions: null);
    }

    [Benchmark]
    public Task TunedParallelPipeline()
    {
        return RunSuccessfulPipelineAsync(
            new PipelinePerformanceOptions
            {
                DefaultSegmentExecution = new PipelineExecutionOptions
                {
                    BoundedCapacity = 10_000,
                    DegreeOfParallelism = Environment.ProcessorCount,
                    EnsureOrdered = false
                }
            });
    }

    [Benchmark]
    public async Task DeadLetterPipeline()
    {
        var tracker = new BenchmarkCompletionTracker();
        var pipeline = Pipeline<BenchmarkRecord>.New("benchmark-in-memory-dead-letter")
            .WithInMemorySource(new object())
            .AddSegment(new FaultingBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark")
            .WithDeadLetterDestination(new InMemoryDeadLetterDestination<BenchmarkRecord>())
            .WithErrorHandler(_ => PipelineErrorAction.DeadLetter)
            .Build();

        tracker.Attach(pipeline);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(pipeline, _faultingRecords).ConfigureAwait(false);
        await tracker.WaitForDeadLetteredAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(pipeline).ConfigureAwait(false);

        if (tracker.DeadLetteredCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} dead-lettered records but observed {tracker.DeadLetteredCount}.");
        }
    }

    private async Task RunSuccessfulPipelineAsync(PipelinePerformanceOptions? performanceOptions)
    {
        var tracker = new BenchmarkCompletionTracker();
        var builder = Pipeline<BenchmarkRecord>.New("benchmark-in-memory")
            .WithInMemorySource(new object())
            .AddSegment(new PassThroughBenchmarkSegment(), new object())
            .WithInMemoryDestination("benchmark");

        if (performanceOptions is not null)
        {
            builder.UsePerformanceOptions(performanceOptions);
        }

        var pipeline = builder.Build();
        tracker.Attach(pipeline);

        await pipeline.StartPipelineAsync().ConfigureAwait(false);
        await BenchmarkPipelineRunner.PublishAsync(pipeline, _successfulRecords).ConfigureAwait(false);
        await tracker.WaitForCompletedAsync(RecordCount, ObservationTimeout).ConfigureAwait(false);
        await BenchmarkPipelineRunner.CompleteAsync(pipeline).ConfigureAwait(false);

        if (tracker.CompletedCount != RecordCount)
        {
            throw new InvalidOperationException(
                $"Expected {RecordCount} completed records but observed {tracker.CompletedCount}.");
        }
    }
}

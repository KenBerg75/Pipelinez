using Pipelinez.Core;

namespace Pipelinez.Benchmarks;

internal static class BenchmarkPipelineRunner
{
    public static async Task PublishAsync(IPipeline<BenchmarkRecord> pipeline, IEnumerable<BenchmarkRecord> records)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(records);

        foreach (var record in records)
        {
            await pipeline.PublishAsync(record).ConfigureAwait(false);
        }
    }

    public static async Task CompleteAsync(IPipeline<BenchmarkRecord> pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        await pipeline.CompleteAsync().ConfigureAwait(false);
        await pipeline.Completion.ConfigureAwait(false);
    }

    public static async Task TryStopAsync(IPipeline<BenchmarkRecord>? pipeline)
    {
        if (pipeline is null)
        {
            return;
        }

        try
        {
            await pipeline.CompleteAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            await pipeline.Completion.ConfigureAwait(false);
        }
        catch
        {
        }
    }
}

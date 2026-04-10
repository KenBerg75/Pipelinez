using Pipelinez.Core.Record;

namespace Pipelinez.Core.Source;

/// <summary>
/// Provides a minimal in-memory source for tests and simple manual publication scenarios.
/// </summary>
public class InMemoryPipelineSource<T> : PipelineSourceBase<T> where T : PipelineRecord
{
    /// <inheritdoc />
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        // nothing to initialize
    }
}

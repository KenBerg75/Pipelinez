using Pipelinez.Core.Record;

namespace Pipelinez.Core.Source;


public class InMemoryPipelineSource<T> : PipelineSourceBase<T> where T : PipelineRecord
{
    protected override async Task MainLoop(CancellationTokenSource cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }
    }

    protected override void Initialize()
    {
        // nothing to initialize
    }
}
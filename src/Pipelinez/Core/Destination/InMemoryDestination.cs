using Microsoft.Extensions.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;


public class InMemoryPipelineDestination<T> : PipelineDestination<T> where T : PipelineRecord
{
    public InMemoryPipelineDestination()
    {
        
    }

    protected override Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        Logger.LogTrace("Record received: {record}", record);
        return Task.CompletedTask;
    }

    protected override void Initialize()
    {
        // No initialization needed
    }
}

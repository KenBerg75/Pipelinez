using Microsoft.Extensions.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;


public class InMemoryPipelineDestination<T> : PipelineDestination<T> where T : PipelineRecord
{
    public InMemoryPipelineDestination()
    {
        
    }

    protected override void ExecuteAsync(T record)
    {
        Logger.LogTrace("Record received: {record}", record);
    }

    protected override void Initialize()
    {
        // No initialization needed
    }
}
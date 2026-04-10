using Microsoft.Extensions.Logging;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Destination;

/// <summary>
/// Provides a minimal in-memory destination that simply logs received records.
/// </summary>
public class InMemoryPipelineDestination<T> : PipelineDestination<T> where T : PipelineRecord
{
    /// <summary>
    /// Initializes a new in-memory destination.
    /// </summary>
    public InMemoryPipelineDestination()
    {
        
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(T record, CancellationToken cancellationToken)
    {
        Logger.LogTrace("Record received: {record}", record);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        // No initialization needed
    }
}

using Pipelinez.Core.Eventing;

namespace Pipelinez.Core;

public interface IPipeline<T>
{
    Task StartAsync(CancellationTokenSource cancellationToken);
    
    Task PublishAsync(T record);

    /// <summary>
    /// Completes and shuts down the pipeline.
    /// </summary>
    Task CompleteAsync();
    
    #region Eventing
    
    /// <summary>
    /// Event that is raised when a message is completed in the pipeline.
    /// </summary>
    event PipelineRecordCompletedEventHandler<T> OnPipelineRecordCompleted;

    #endregion
}

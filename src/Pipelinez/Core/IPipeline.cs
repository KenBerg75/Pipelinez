using Pipelinez.Core.Eventing;
using Pipelinez.Core.Status;

namespace Pipelinez.Core;

public interface IPipeline<T>
{
    /// <summary>
    /// Starts up the pipeline
    /// </summary>
    /// <param name="cancellationToken">A token allowing the cancellation of the pipeline</param>
    /// <returns></returns>
    void StartPipelineAsync(CancellationTokenSource cancellationToken);
    
    /// <summary>
    /// Publishes a record into the pipeline. Acts as a manual source for the pipeline.
    /// </summary>
    /// <returns></returns>
    Task PublishAsync(T record);

    /// <summary>
    /// Completes and shuts down the pipeline.
    /// </summary>
    Task CompleteAsync();

    /// <summary>Gets a <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation and completion of the pipeline.</summary>
    /// <returns>The task.</returns>
    Task Completion { get; }

    PipelineStatus GetStatus();
    
    #region Eventing
    
    /// <summary>
    /// Event that is raised when a message is completed in the pipeline.
    /// </summary>
    event PipelineRecordCompletedEventHandler<T> OnPipelineRecordCompleted;

    #endregion
}

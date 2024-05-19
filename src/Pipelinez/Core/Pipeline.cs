using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Eventing;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;

namespace Pipelinez.Core;

public class Pipeline<TPipelineRecord> : IPipeline<TPipelineRecord> where TPipelineRecord : PipelineRecord
{
    #region Builder
    
    /// <summary>
    /// Initiates the build of a new pipeline with the specified PipelineRecord type
    /// </summary>
    /// <param name="pipelineName">Name of the pipeline</param>
    /// <typeparam name="TPipelineRecord">Type of the record that will flow through the pipeline</typeparam>
    /// <returns></returns>
    public static PipelineBuilder<TPipelineRecord> New(string pipelineName)
    {
        return new PipelineBuilder<TPipelineRecord>(pipelineName);
    }
    
    #endregion
    
    # region Pipeline Components

    private readonly string _name;
    private readonly IPipelineSource<TPipelineRecord> _source;
    private readonly IList<IPipelineSegment<TPipelineRecord>> _segments;
    private readonly IPipelineDestination<TPipelineRecord> _destination;
    
    #endregion
    
    #region Logging
    
    protected ILogger<Pipeline<TPipelineRecord>> Logger { get; }
    
    #endregion
    
    #region Pipeline Execution

    private Task _sourceExecution;
    private Task _destinationExecution;
    
    #endregion
    
    #region Eventing

    /// <summary>
    /// Occurs when a record in the pipeline has completed traversing the pipeline
    /// </summary>
    public event PipelineRecordCompletedEventHandler<TPipelineRecord>? OnPipelineRecordCompleted;

    /// <summary>
    /// Occurs when the pipeline container has completed traversing the pipeline
    /// </summary>
    internal event PipelineContainerCompletedEventHandler<PipelineContainer<TPipelineRecord>>
        OnPipelineContainerCompelted; 
    
    /// <summary>
    /// Triggers a PipelineContainerCompleted event
    /// </summary>
    internal void TriggerPipelineEvent(PipelineContainerCompletedEventHandlerArgs<PipelineContainer<TPipelineRecord>> evt)
    {
        // Container completed does 2 things: 
        // 1 - lets the source know that the record has completed so it can apply any transactional commits if needed
        // 2 - lets pipeline users know that the record has completed
        OnPipelineContainerCompelted?.Invoke(this, evt);
        OnPipelineRecordCompleted?.Invoke(this, new PipelineRecordCompletedEventHandlerArgs<TPipelineRecord>(evt.Container.Record));
    }

    #endregion

    #region Construction
    
    internal Pipeline(string name, IPipelineSource<TPipelineRecord> source, IPipelineDestination<TPipelineRecord> destination, 
        IList<IPipelineSegment<TPipelineRecord>> segments)
    {
        Guard.Against.NullOrEmpty(name, message: "Pipeline must have a name");
        Guard.Against.Null(source, message: "Pipeline must have a valid source");
        Guard.Against.Null(destination, message: "Pipeline must have a valid destination");

        this.Logger = LoggingManager.Instance.CreateLogger<Pipeline<TPipelineRecord>>();
        
        this._name = name;
        this._source = source;
        this._destination = destination;
        this._segments = segments;
    }

    internal void LinkPipeline()
    {
        Guard.Against.Null(this._source, message: "Pipeline cannot be initialized. No Source defined");
        Guard.Against.Null(this._destination, message: "Pipeline cannot be initialized. No Destination defined");

        var options =  new DataflowLinkOptions() { MaxMessages = DataflowBlockOptions.Unbounded, PropagateCompletion = true };
        
        if (_segments.Any())
        {
            for (int i = 0; i < this._segments.Count; i++)
            {
                if (i == 0)
                { this._source.ConnectTo(this._segments[i], options); }
                else
                { this._segments[i - 1].ConnectTo(this._segments[i], options); }
            }

            _segments.Last().ConnectTo(_destination, options);
        }
        else
        {
            _source.ConnectTo(_destination, options);
        }

    }

    /// <summary>
    /// Allows for initialization of pipeline components
    /// </summary>
    internal void InitializePipeline()
    {
        // Allow for components of the pipeline to initialize
        _source.Initialize(this);
        _destination.Initialize(this);
    }

    #endregion
    
    // Initiates the pipeline
    public async Task StartAsync(CancellationTokenSource cancellationToken)
    {
        _sourceExecution = _source.StartAsync(cancellationToken);
        _destinationExecution = _destination.StartAsync(cancellationToken);
        Logger?.LogInformation("$Pipeline started: {PipelineName}", _name);
    }

    public async Task PublishAsync(TPipelineRecord record)
    {
        await _source.PublishAsync(record);
    }

    public async Task CompleteAsync()
    {
        _source.Complete();

        _destination.Completion.Wait(CancellationToken.None);
    }
}
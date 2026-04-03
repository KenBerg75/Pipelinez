using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.Destination;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;

namespace Pipelinez.Core;

public class PipelineBuilder<T>(string pipelineName)
    where T : PipelineRecord
{
    public string PipelineName { get; } = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));

    #region Pipeline Components

    private IPipelineSource<T>? _source;
    private IList<IPipelineSegment<T>> _segments = new List<IPipelineSegment<T>>();
    private IPipelineDestination<T>? _destination;
    private PipelineErrorHandler<T>? _errorHandler;
    private PipelineHostOptions _hostOptions = new();
    
    #endregion
    
    #region Sources

    public PipelineBuilder<T> WithSource(IPipelineSource<T> source)
    {
        _source = Guard.Against.Null(source, nameof(source));
        return this;
    }
    
    public PipelineBuilder<T> WithInMemorySource(object config)
    {
        return WithSource(new InMemoryPipelineSource<T>());
    }
    
    #endregion

    #region Segments
    
    public PipelineBuilder<T> AddSegment(IPipelineSegment<T> segment, object config) 
    {
        _segments.Add(segment);
        return this;
    }
    
    #endregion

    #region Destinations

    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        return this;
    }
    
    public PipelineBuilder<T> WithInMemoryDestination(string config)
    {
        return WithDestination(new InMemoryPipelineDestination<T>());
    }
    
    #endregion
    
    #region Logging
    
    public PipelineBuilder<T> UseLogger(ILoggerFactory logFactory)
    {
        LoggingManager.Instance.AssignLogFactory(logFactory);
        return this;
    }
    
    #endregion

    #region Hosting

    public PipelineBuilder<T> UseHostOptions(PipelineHostOptions options)
    {
        _hostOptions = Guard.Against.Null(options, nameof(options));
        return this;
    }

    #endregion
    
    #region Error Handling
    
    public PipelineBuilder<T> WithErrorHandler(PipelineErrorHandler<T> errorHandler)
    {
        _errorHandler = Guard.Against.Null(errorHandler, nameof(errorHandler));
        return this;
    }

    public PipelineBuilder<T> WithErrorHandler(Func<PipelineErrorContext<T>, PipelineErrorAction> errorHandler)
    {
        Guard.Against.Null(errorHandler, nameof(errorHandler));
        _errorHandler = context => Task.FromResult(errorHandler(context));
        return this;
    }
    
    #endregion
    
    #region Build

    public IPipeline<T> Build()
    {
        // Validate that we have at least a source and destination
        Guard.Against.Null(this._source, message: "Pipeline must have a source defined before it is built");
        Guard.Against.Null(this._destination, message: "Pipeline must have a destination defined before it is built");

        if (_hostOptions.ExecutionMode == PipelineExecutionMode.Distributed)
        {
            if (_source is not IDistributedPipelineSource<T> distributedSource || !distributedSource.SupportsDistributedExecution)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{PipelineName}' is configured for distributed execution, but source '{_source.GetType().Name}' does not support distributed ownership.");
            }
        }
        
        // Create a 'Pipeline' object passing all components and linking them
        // i.e. build the pipeline by linking source->segments->destination
        var pipeline = new Pipeline<T>(pipelineName, this._source, this._destination, this._segments, _errorHandler, _hostOptions);
        pipeline.LinkPipeline();
        pipeline.InitializePipeline();
        //return the pipeline
        return pipeline;
    }
    
    #endregion
}



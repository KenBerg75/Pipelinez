using Ardalis.GuardClauses;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Destination;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Record;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;

namespace Pipelinez.Core;

public partial class PipelineBuilder<T>(string pipelineName)
    where T : PipelineRecord
{
    #region Pipeline Components

    private IPipelineSource<T> _source;
    private IList<IPipelineSegment<T>> _segments = new List<IPipelineSegment<T>>();
    private IPipelineDestination<T> _destination;
    
    #endregion
    
    #region Sources
    
    public PipelineBuilder<T> WithInMemorySource(object config)
    {
        _source = new InMemoryPipelineSource<T>();
        return this;
    }
    /*
    public PipelineBuilder<T> WithKafkaSource<TRecordKey, TRecordValue>(string config,
        Func<TRecordKey, TRecordValue, T> map)
    {
        throw new NotImplementedException();
    }
    */
    
    #endregion

    #region Segments
    
    public PipelineBuilder<T> AddSegment(IPipelineSegment<T> segment, object config) 
    {
        _segments.Add(segment);
        return this;
    }
    
    #endregion

    #region Destinations
    
    public PipelineBuilder<T> WithInMemoryDestination(string config)
    {
        _destination = new InMemoryPipelineDestination<T>();
        return this;
    }
    
    
    public PipelineBuilder<T> WithKafkaDestination<TRecordKey, TRecordValue>(string config,
        Func<T, Message<TRecordKey, TRecordValue>> map)
    {
        throw new NotImplementedException();
    }
    
    #endregion
    
    #region Logging
    
    public PipelineBuilder<T> UseLogger(ILoggerFactory logFactory)
    {
        LoggingManager.Instance.AssignLogFactory(logFactory);
        return this;
    }
    
    #endregion
    
    #region Error Handling
    
    public PipelineBuilder<T> WithErrorHandler(Func<Exception, PipelineContainer<T>, bool> errorHandler)
    {
        throw new NotImplementedException();
    }
    
    #endregion
    
    #region Build

    public IPipeline<T> Build()
    {
        // Validate that we have at least a source and destination
        Guard.Against.Null(this._source, message: "Pipeline must have a source defined before it is built");
        Guard.Against.Null(this._destination, message: "Pipeline must have a destination defined before it is built");
        
        // Create a 'Pipeline' object passing all components and linking them
        // i.e. build the pipeline by linking source->segments->destination
        var pipeline = new Pipeline<T>(pipelineName, this._source, this._destination, this._segments);
        pipeline.LinkPipeline();
        pipeline.InitializePipeline();
        //return the pipeline
        return pipeline;
    }
    
    #endregion
}



using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Destination;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Retry;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;

namespace Pipelinez.Core;

public class PipelineBuilder<T>(string pipelineName)
    where T : PipelineRecord
{
    private sealed record PipelineSegmentRegistration(
        IPipelineSegment<T> Segment,
        PipelineExecutionOptions? ExecutionOptions,
        PipelineRetryPolicy<T>? RetryPolicy);

    public string PipelineName { get; } = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));

    #region Pipeline Components

    private IPipelineSource<T>? _source;
    private PipelineExecutionOptions? _sourceExecutionOptions;
    private readonly IList<PipelineSegmentRegistration> _segments = new List<PipelineSegmentRegistration>();
    private IPipelineDestination<T>? _destination;
    private PipelineExecutionOptions? _destinationExecutionOptions;
    private PipelineRetryPolicy<T>? _destinationRetryPolicy;
    private IPipelineDeadLetterDestination<T>? _deadLetterDestination;
    private PipelineErrorHandler<T>? _errorHandler;
    private PipelineHostOptions _hostOptions = new();
    private PipelineDeadLetterOptions _deadLetterOptions = new();
    private PipelineFlowControlOptions _flowControlOptions = new();
    private PipelinePerformanceOptions _performanceOptions = new();
    private PipelineRetryOptions<T> _retryOptions = new();

    #endregion

    #region Sources

    public PipelineBuilder<T> WithSource(IPipelineSource<T> source)
    {
        _source = Guard.Against.Null(source, nameof(source));
        _sourceExecutionOptions = null;
        return this;
    }

    public PipelineBuilder<T> WithSource(IPipelineSource<T> source, PipelineExecutionOptions executionOptions)
    {
        _source = Guard.Against.Null(source, nameof(source));
        _sourceExecutionOptions = Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate();
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
        _segments.Add(new PipelineSegmentRegistration(
            Guard.Against.Null(segment, nameof(segment)),
            null,
            null));
        return this;
    }

    public PipelineBuilder<T> AddSegment(
        IPipelineSegment<T> segment,
        object config,
        PipelineExecutionOptions executionOptions)
    {
        _segments.Add(new PipelineSegmentRegistration(
            Guard.Against.Null(segment, nameof(segment)),
            Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate(),
            null));
        return this;
    }

    public PipelineBuilder<T> AddSegment(
        IPipelineSegment<T> segment,
        object config,
        PipelineRetryPolicy<T> retryPolicy)
    {
        _segments.Add(new PipelineSegmentRegistration(
            Guard.Against.Null(segment, nameof(segment)),
            null,
            Guard.Against.Null(retryPolicy, nameof(retryPolicy))));
        return this;
    }

    public PipelineBuilder<T> AddSegment(
        IPipelineSegment<T> segment,
        object config,
        PipelineExecutionOptions executionOptions,
        PipelineRetryPolicy<T> retryPolicy)
    {
        _segments.Add(new PipelineSegmentRegistration(
            Guard.Against.Null(segment, nameof(segment)),
            Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate(),
            Guard.Against.Null(retryPolicy, nameof(retryPolicy))));
        return this;
    }

    #endregion

    #region Destinations

    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = null;
        _destinationRetryPolicy = null;
        return this;
    }

    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination, PipelineExecutionOptions executionOptions)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate();
        _destinationRetryPolicy = null;
        return this;
    }

    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination, PipelineRetryPolicy<T> retryPolicy)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = null;
        _destinationRetryPolicy = Guard.Against.Null(retryPolicy, nameof(retryPolicy));
        return this;
    }

    public PipelineBuilder<T> WithDestination(
        IPipelineDestination<T> destination,
        PipelineExecutionOptions executionOptions,
        PipelineRetryPolicy<T> retryPolicy)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate();
        _destinationRetryPolicy = Guard.Against.Null(retryPolicy, nameof(retryPolicy));
        return this;
    }

    public PipelineBuilder<T> WithInMemoryDestination(string config)
    {
        return WithDestination(new InMemoryPipelineDestination<T>());
    }

    public PipelineBuilder<T> WithDeadLetterDestination(IPipelineDeadLetterDestination<T> deadLetterDestination)
    {
        _deadLetterDestination = Guard.Against.Null(deadLetterDestination, nameof(deadLetterDestination));
        return this;
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

    public PipelineBuilder<T> UseDeadLetterOptions(PipelineDeadLetterOptions options)
    {
        _deadLetterOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    public PipelineBuilder<T> UseHostOptions(PipelineHostOptions options)
    {
        _hostOptions = Guard.Against.Null(options, nameof(options));
        return this;
    }

    #endregion

    #region Flow Control

    public PipelineBuilder<T> UseFlowControlOptions(PipelineFlowControlOptions options)
    {
        _flowControlOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    #endregion

    #region Performance

    public PipelineBuilder<T> UsePerformanceOptions(PipelinePerformanceOptions options)
    {
        _performanceOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    #endregion

    #region Retry

    public PipelineBuilder<T> UseRetryOptions(PipelineRetryOptions<T> options)
    {
        _retryOptions = Guard.Against.Null(options, nameof(options));
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
        Guard.Against.Null(_source, message: "Pipeline must have a source defined before it is built");
        Guard.Against.Null(_destination, message: "Pipeline must have a destination defined before it is built");

        if (_hostOptions.ExecutionMode == PipelineExecutionMode.Distributed)
        {
            if (_source is not IDistributedPipelineSource<T> distributedSource || !distributedSource.SupportsDistributedExecution)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{PipelineName}' is configured for distributed execution, but source '{_source.GetType().Name}' does not support distributed ownership.");
            }
        }

        var performanceCollector = new PipelinePerformanceCollector(_performanceOptions.Metrics);

        ApplyExecutionOptions(
            "source",
            _source,
            _sourceExecutionOptions ?? _performanceOptions.SourceExecution);
        ApplyPerformanceCollector(
            _source,
            performanceCollector,
            $"Source:{_source.GetType().Name}");

        for (var i = 0; i < _segments.Count; i++)
        {
            var registration = _segments[i];
            ApplyExecutionOptions(
                "segment",
                registration.Segment,
                registration.ExecutionOptions ?? _performanceOptions.DefaultSegmentExecution);
            ApplyRetryOptions(
                "segment",
                registration.Segment,
                registration.RetryPolicy ?? _retryOptions.DefaultSegmentPolicy);
            ApplyPerformanceCollector(
                registration.Segment,
                performanceCollector,
                $"Segment[{i}]:{registration.Segment.GetType().Name}");
        }

        ApplyExecutionOptions(
            "destination",
            _destination,
            _destinationExecutionOptions ?? _performanceOptions.DestinationExecution);
        ApplyRetryOptions(
            "destination",
            _destination,
            _destinationRetryPolicy ?? _retryOptions.DestinationPolicy);
        ApplyBatchingOptions(_destination, _performanceOptions.DestinationBatching);
        ApplyPerformanceCollector(
            _destination,
            performanceCollector,
            $"Destination:{_destination.GetType().Name}");

        var pipeline = new Pipeline<T>(
            pipelineName,
            _source,
            _destination,
            _segments.Select(registration => registration.Segment).ToList(),
            _errorHandler,
            _hostOptions,
            _deadLetterDestination,
            _deadLetterOptions,
            _flowControlOptions,
            performanceCollector,
            _retryOptions.EmitRetryEvents);
        pipeline.LinkPipeline();
        pipeline.InitializePipeline();
        return pipeline;
    }

    private static void ApplyExecutionOptions<TComponent>(
        string componentRole,
        TComponent component,
        PipelineExecutionOptions executionOptions)
    {
        if (component is IPipelineExecutionConfigurable configurable)
        {
            configurable.ConfigureExecutionOptions(executionOptions);
            return;
        }

        throw new InvalidOperationException(
            $"Pipeline {componentRole} '{component?.GetType().Name}' does not support execution tuning.");
    }

    private static void ApplyPerformanceCollector<TComponent>(
        TComponent component,
        IPipelinePerformanceCollector performanceCollector,
        string componentName)
    {
        if (component is IPipelinePerformanceAware performanceAware)
        {
            performanceAware.ConfigurePerformanceCollector(performanceCollector, componentName);
        }
    }

    private static void ApplyRetryOptions<TComponent>(
        string componentRole,
        TComponent component,
        PipelineRetryPolicy<T>? retryPolicy)
    {
        if (retryPolicy is null)
        {
            return;
        }

        if (component is IPipelineRetryConfigurable<T> configurable)
        {
            configurable.ConfigureRetryPolicy(retryPolicy);
            return;
        }

        throw new InvalidOperationException(
            $"Pipeline {componentRole} '{component?.GetType().Name}' does not support retry policies.");
    }

    private static void ApplyBatchingOptions<TComponent>(
        TComponent component,
        PipelineBatchingOptions? batchingOptions)
    {
        if (batchingOptions is null)
        {
            return;
        }

        if (component is not IBatchedPipelineDestination<T>)
        {
            throw new InvalidOperationException(
                $"Destination '{component?.GetType().Name}' does not support batch execution, but destination batching was configured.");
        }

        if (component is IPipelineBatchingAware batchingAware)
        {
            batchingAware.ConfigureBatchingOptions(batchingOptions.Validate());
        }
    }

    #endregion
}

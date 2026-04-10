using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Pipelinez.Core.Distributed;
using Pipelinez.Core.DeadLettering;
using Pipelinez.Core.Destination;
using Pipelinez.Core.ErrorHandling;
using Pipelinez.Core.FlowControl;
using Pipelinez.Core.Logging;
using Pipelinez.Core.Operational;
using Pipelinez.Core.Performance;
using Pipelinez.Core.Record;
using Pipelinez.Core.Retry;
using Pipelinez.Core.Segment;
using Pipelinez.Core.Source;

namespace Pipelinez.Core;

/// <summary>
/// Builds a Pipelinez pipeline by composing a source, zero or more segments, and a destination.
/// </summary>
/// <typeparam name="T">The pipeline record type processed by the pipeline.</typeparam>
public class PipelineBuilder<T>(string pipelineName)
    where T : PipelineRecord
{
    private sealed record PipelineSegmentRegistration(
        IPipelineSegment<T> Segment,
        PipelineExecutionOptions? ExecutionOptions,
        PipelineRetryPolicy<T>? RetryPolicy);

    /// <summary>
    /// Gets the logical name of the pipeline being built.
    /// </summary>
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
    private PipelineOperationalOptions _operationalOptions = new();
    private PipelinePerformanceOptions _performanceOptions = new();
    private PipelineRetryOptions<T> _retryOptions = new();

    #endregion

    #region Sources

    /// <summary>
    /// Configures the pipeline source.
    /// </summary>
    /// <param name="source">The source to use.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithSource(IPipelineSource<T> source)
    {
        _source = Guard.Against.Null(source, nameof(source));
        _sourceExecutionOptions = null;
        return this;
    }

    /// <summary>
    /// Configures the pipeline source with explicit execution options.
    /// </summary>
    /// <param name="source">The source to use.</param>
    /// <param name="executionOptions">The execution options to apply to the source.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithSource(IPipelineSource<T> source, PipelineExecutionOptions executionOptions)
    {
        _source = Guard.Against.Null(source, nameof(source));
        _sourceExecutionOptions = Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate();
        return this;
    }

    /// <summary>
    /// Configures the built-in in-memory source.
    /// </summary>
    /// <param name="config">Reserved source configuration input.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithInMemorySource(object config)
    {
        return WithSource(new InMemoryPipelineSource<T>());
    }

    #endregion

    #region Segments

    /// <summary>
    /// Adds a segment to the pipeline.
    /// </summary>
    /// <param name="segment">The segment to add.</param>
    /// <param name="config">Reserved segment configuration input.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> AddSegment(IPipelineSegment<T> segment, object config)
    {
        _segments.Add(new PipelineSegmentRegistration(
            Guard.Against.Null(segment, nameof(segment)),
            null,
            null));
        return this;
    }

    /// <summary>
    /// Adds a segment to the pipeline with explicit execution options.
    /// </summary>
    /// <param name="segment">The segment to add.</param>
    /// <param name="config">Reserved segment configuration input.</param>
    /// <param name="executionOptions">The execution options to apply to the segment.</param>
    /// <returns>The builder instance.</returns>
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

    /// <summary>
    /// Adds a segment to the pipeline with an explicit retry policy.
    /// </summary>
    /// <param name="segment">The segment to add.</param>
    /// <param name="config">Reserved segment configuration input.</param>
    /// <param name="retryPolicy">The retry policy to apply to the segment.</param>
    /// <returns>The builder instance.</returns>
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

    /// <summary>
    /// Adds a segment to the pipeline with explicit execution options and retry policy.
    /// </summary>
    /// <param name="segment">The segment to add.</param>
    /// <param name="config">Reserved segment configuration input.</param>
    /// <param name="executionOptions">The execution options to apply to the segment.</param>
    /// <param name="retryPolicy">The retry policy to apply to the segment.</param>
    /// <returns>The builder instance.</returns>
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

    /// <summary>
    /// Configures the pipeline destination.
    /// </summary>
    /// <param name="destination">The destination to use.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = null;
        _destinationRetryPolicy = null;
        return this;
    }

    /// <summary>
    /// Configures the pipeline destination with explicit execution options.
    /// </summary>
    /// <param name="destination">The destination to use.</param>
    /// <param name="executionOptions">The execution options to apply to the destination.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination, PipelineExecutionOptions executionOptions)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = Guard.Against.Null(executionOptions, nameof(executionOptions)).Validate();
        _destinationRetryPolicy = null;
        return this;
    }

    /// <summary>
    /// Configures the pipeline destination with an explicit retry policy.
    /// </summary>
    /// <param name="destination">The destination to use.</param>
    /// <param name="retryPolicy">The retry policy to apply to the destination.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithDestination(IPipelineDestination<T> destination, PipelineRetryPolicy<T> retryPolicy)
    {
        _destination = Guard.Against.Null(destination, nameof(destination));
        _destinationExecutionOptions = null;
        _destinationRetryPolicy = Guard.Against.Null(retryPolicy, nameof(retryPolicy));
        return this;
    }

    /// <summary>
    /// Configures the pipeline destination with explicit execution options and retry policy.
    /// </summary>
    /// <param name="destination">The destination to use.</param>
    /// <param name="executionOptions">The execution options to apply to the destination.</param>
    /// <param name="retryPolicy">The retry policy to apply to the destination.</param>
    /// <returns>The builder instance.</returns>
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

    /// <summary>
    /// Configures the built-in in-memory destination.
    /// </summary>
    /// <param name="config">Reserved destination configuration input.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithInMemoryDestination(string config)
    {
        return WithDestination(new InMemoryPipelineDestination<T>());
    }

    /// <summary>
    /// Configures the dead-letter destination used for terminally handled faults.
    /// </summary>
    /// <param name="deadLetterDestination">The dead-letter destination to use.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithDeadLetterDestination(IPipelineDeadLetterDestination<T> deadLetterDestination)
    {
        _deadLetterDestination = Guard.Against.Null(deadLetterDestination, nameof(deadLetterDestination));
        return this;
    }

    #endregion

    #region Logging

    /// <summary>
    /// Configures the logger factory used by pipeline components.
    /// </summary>
    /// <param name="logFactory">The logger factory to use.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UseLogger(ILoggerFactory logFactory)
    {
        LoggingManager.Instance.AssignLogFactory(logFactory);
        return this;
    }

    #endregion

    #region Hosting

    /// <summary>
    /// Configures dead-letter behavior for the pipeline.
    /// </summary>
    /// <param name="options">The dead-letter options to apply.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UseDeadLetterOptions(PipelineDeadLetterOptions options)
    {
        _deadLetterOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    /// <summary>
    /// Configures host-level execution options.
    /// </summary>
    /// <param name="options">The host options to apply.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UseHostOptions(PipelineHostOptions options)
    {
        _hostOptions = Guard.Against.Null(options, nameof(options));
        return this;
    }

    #endregion

    #region Flow Control

    /// <summary>
    /// Configures operational tooling behavior for the pipeline.
    /// </summary>
    /// <param name="options">The operational options to apply.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UseOperationalOptions(PipelineOperationalOptions options)
    {
        _operationalOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    /// <summary>
    /// Configures pipeline-wide flow-control behavior.
    /// </summary>
    /// <param name="options">The flow-control options to apply.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UseFlowControlOptions(PipelineFlowControlOptions options)
    {
        _flowControlOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    #endregion

    #region Performance

    /// <summary>
    /// Configures performance and execution-tuning options for the pipeline.
    /// </summary>
    /// <param name="options">The performance options to apply.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UsePerformanceOptions(PipelinePerformanceOptions options)
    {
        _performanceOptions = Guard.Against.Null(options, nameof(options)).Validate();
        return this;
    }

    #endregion

    #region Retry

    /// <summary>
    /// Configures default retry behavior for segments and destinations.
    /// </summary>
    /// <param name="options">The retry options to apply.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> UseRetryOptions(PipelineRetryOptions<T> options)
    {
        _retryOptions = Guard.Against.Null(options, nameof(options));
        return this;
    }

    #endregion

    #region Error Handling

    /// <summary>
    /// Configures the asynchronous error handler used after retries are exhausted.
    /// </summary>
    /// <param name="errorHandler">The error handler to use.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithErrorHandler(PipelineErrorHandler<T> errorHandler)
    {
        _errorHandler = Guard.Against.Null(errorHandler, nameof(errorHandler));
        return this;
    }

    /// <summary>
    /// Configures the synchronous error handler used after retries are exhausted.
    /// </summary>
    /// <param name="errorHandler">The error handler to use.</param>
    /// <returns>The builder instance.</returns>
    public PipelineBuilder<T> WithErrorHandler(Func<PipelineErrorContext<T>, PipelineErrorAction> errorHandler)
    {
        Guard.Against.Null(errorHandler, nameof(errorHandler));
        _errorHandler = context => Task.FromResult(errorHandler(context));
        return this;
    }

    #endregion

    #region Build

    /// <summary>
    /// Builds the pipeline using the configured components and options.
    /// </summary>
    /// <returns>The built pipeline instance.</returns>
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
            _operationalOptions,
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

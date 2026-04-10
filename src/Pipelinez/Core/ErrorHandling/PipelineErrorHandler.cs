using Pipelinez.Core.Record;

namespace Pipelinez.Core.ErrorHandling;

/// <summary>
/// Represents asynchronous terminal fault policy logic for a faulted pipeline record.
/// </summary>
/// <typeparam name="T">The pipeline record type being handled.</typeparam>
/// <param name="context">The fault context passed to the handler.</param>
/// <returns>The error action the pipeline should take.</returns>
public delegate Task<PipelineErrorAction> PipelineErrorHandler<T>(PipelineErrorContext<T> context)
    where T : PipelineRecord;

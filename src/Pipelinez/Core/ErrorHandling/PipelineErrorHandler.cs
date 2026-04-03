using Pipelinez.Core.Record;

namespace Pipelinez.Core.ErrorHandling;

public delegate Task<PipelineErrorAction> PipelineErrorHandler<T>(PipelineErrorContext<T> context)
    where T : PipelineRecord;

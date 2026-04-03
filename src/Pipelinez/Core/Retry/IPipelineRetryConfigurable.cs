using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

public interface IPipelineRetryConfigurable<T> where T : PipelineRecord
{
    void ConfigureRetryPolicy(PipelineRetryPolicy<T>? retryPolicy);

    PipelineRetryPolicy<T>? GetRetryPolicy();
}

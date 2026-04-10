using Pipelinez.Core.Record;

namespace Pipelinez.Core.Retry;

/// <summary>
/// Defines a component that can be configured with a retry policy.
/// </summary>
/// <typeparam name="T">The pipeline record type handled by the component.</typeparam>
public interface IPipelineRetryConfigurable<T> where T : PipelineRecord
{
    /// <summary>
    /// Applies the retry policy that should be used for transient failures.
    /// </summary>
    /// <param name="retryPolicy">The retry policy to use, or <see langword="null" /> to disable retries.</param>
    void ConfigureRetryPolicy(PipelineRetryPolicy<T>? retryPolicy);

    /// <summary>
    /// Gets the retry policy currently configured for the component.
    /// </summary>
    /// <returns>The configured retry policy, or <see langword="null" /> when retries are disabled.</returns>
    PipelineRetryPolicy<T>? GetRetryPolicy();
}

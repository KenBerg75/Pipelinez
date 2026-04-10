using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pipelinez.Core.Record;

namespace Pipelinez.Core.Operational;

/// <summary>
/// Adapts a Pipelinez pipeline into a standard ASP.NET Core health check.
/// </summary>
/// <typeparam name="T">The pipeline record type handled by the pipeline.</typeparam>
public sealed class PipelineHealthCheck<T> : IHealthCheck
    where T : PipelineRecord
{
    private readonly IPipeline<T> _pipeline;
    private readonly bool _includeComponentDiagnostics;

    /// <summary>
    /// Initializes a new pipeline health check.
    /// </summary>
    /// <param name="pipeline">The pipeline to inspect.</param>
    /// <param name="includeComponentDiagnostics">
    /// A value indicating whether component status information should be included in the health-check payload.
    /// </param>
    public PipelineHealthCheck(IPipeline<T> pipeline, bool includeComponentDiagnostics = true)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _includeComponentDiagnostics = includeComponentDiagnostics;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var health = _pipeline.GetHealthStatus();
        var data = new Dictionary<string, object>
        {
            ["pipeline.name"] = health.PipelineName,
            ["pipeline.health_state"] = health.State.ToString(),
            ["pipeline.reason"] = health.Reason ?? string.Empty,
            ["pipeline.records_per_second"] = health.Performance.RecordsPerSecond,
            ["pipeline.total_faulted"] = health.Performance.TotalRecordsFaulted,
            ["pipeline.total_deadlettered"] = health.Performance.TotalDeadLetteredCount,
            ["pipeline.total_publish_rejected"] = health.Performance.TotalPublishRejectedCount
        };

        if (_includeComponentDiagnostics)
        {
            data["pipeline.components"] = string.Join(
                ",",
                health.RuntimeStatus.Components.Select(component => $"{component.Name}:{component.Status}"));
        }

        var result = health.State switch
        {
            PipelineHealthState.Starting => HealthCheckResult.Healthy("Pipeline is starting.", data: data),
            PipelineHealthState.Healthy => HealthCheckResult.Healthy("Pipeline is healthy.", data),
            PipelineHealthState.Completed => HealthCheckResult.Healthy("Pipeline completed successfully.", data),
            PipelineHealthState.Degraded => HealthCheckResult.Degraded(health.Reason ?? "Pipeline is degraded.", data: data),
            PipelineHealthState.Unhealthy => HealthCheckResult.Unhealthy(
                health.Reason ?? "Pipeline is unhealthy.",
                health.LastPipelineFault?.Exception,
                data),
            _ => HealthCheckResult.Unhealthy("Unknown pipeline health state.", data: data)
        };

        return Task.FromResult(result);
    }
}

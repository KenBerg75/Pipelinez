using Ardalis.GuardClauses;

namespace Pipelinez.Core.Operational;

/// <summary>
/// Captures diagnostic identifiers associated with a record as it moves through the pipeline.
/// </summary>
public sealed class PipelineRecordDiagnosticContext
{
    /// <summary>
    /// Initializes a new record diagnostic context.
    /// </summary>
    public PipelineRecordDiagnosticContext(
        string correlationId,
        string pipelineName,
        string instanceId,
        string workerId,
        string? componentName = null)
    {
        CorrelationId = Guard.Against.NullOrWhiteSpace(correlationId, nameof(correlationId));
        PipelineName = Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
        InstanceId = Guard.Against.NullOrWhiteSpace(instanceId, nameof(instanceId));
        WorkerId = Guard.Against.NullOrWhiteSpace(workerId, nameof(workerId));
        ComponentName = componentName;
    }

    /// <summary>
    /// Gets the record correlation identifier.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// Gets the host instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the worker identifier.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Gets the component name associated with the current diagnostic event, if one exists.
    /// </summary>
    public string? ComponentName { get; }
}

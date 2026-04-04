using Ardalis.GuardClauses;

namespace Pipelinez.Core.Operational;

public sealed class PipelineRecordDiagnosticContext
{
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

    public string CorrelationId { get; }

    public string PipelineName { get; }

    public string InstanceId { get; }

    public string WorkerId { get; }

    public string? ComponentName { get; }
}

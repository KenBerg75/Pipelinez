namespace Pipelinez.Core.DeadLettering;

public sealed class PipelineDeadLetterOptions
{
    public bool EmitDeadLetterEvents { get; init; } = true;

    public bool TreatDeadLetterFailureAsPipelineFault { get; init; } = true;

    public bool CloneMetadata { get; init; } = true;

    public PipelineDeadLetterOptions Validate()
    {
        return this;
    }
}

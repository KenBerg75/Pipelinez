namespace Pipelinez.Core.ErrorHandling;

public enum PipelineErrorAction
{
    StopPipeline,
    SkipRecord,
    Rethrow,
    DeadLetter
}

namespace Pipelinez.Core.Performance;

public interface IPipelineExecutionConfigurable
{
    void ConfigureExecutionOptions(PipelineExecutionOptions options);

    PipelineExecutionOptions GetExecutionOptions();
}

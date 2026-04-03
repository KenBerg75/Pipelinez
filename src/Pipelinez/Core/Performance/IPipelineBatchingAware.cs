namespace Pipelinez.Core.Performance;

internal interface IPipelineBatchingAware
{
    void ConfigureBatchingOptions(PipelineBatchingOptions? batchingOptions);
}

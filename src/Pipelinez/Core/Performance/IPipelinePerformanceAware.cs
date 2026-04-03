namespace Pipelinez.Core.Performance;

internal interface IPipelinePerformanceAware
{
    void ConfigurePerformanceCollector(IPipelinePerformanceCollector performanceCollector, string componentName);
}

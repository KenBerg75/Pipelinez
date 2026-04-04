namespace Pipelinez.Core.Operational;

public enum PipelineHealthState
{
    Starting = 0,
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3,
    Completed = 4
}

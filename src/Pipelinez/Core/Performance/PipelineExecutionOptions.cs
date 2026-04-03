using System.Threading.Tasks.Dataflow;
using Ardalis.GuardClauses;

namespace Pipelinez.Core.Performance;

public sealed class PipelineExecutionOptions
{
    public static PipelineExecutionOptions CreateDefaultSourceOptions()
    {
        return new PipelineExecutionOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded,
            DegreeOfParallelism = 1,
            EnsureOrdered = true
        };
    }

    public static PipelineExecutionOptions CreateDefaultSegmentOptions()
    {
        return new PipelineExecutionOptions
        {
            BoundedCapacity = 10_000,
            DegreeOfParallelism = 1,
            EnsureOrdered = true
        };
    }

    public static PipelineExecutionOptions CreateDefaultDestinationOptions()
    {
        return new PipelineExecutionOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded,
            DegreeOfParallelism = 1,
            EnsureOrdered = true
        };
    }

    public int BoundedCapacity { get; init; } = DataflowBlockOptions.Unbounded;

    public int DegreeOfParallelism { get; init; } = 1;

    public bool EnsureOrdered { get; init; } = true;

    public PipelineExecutionOptions Validate()
    {
        if (BoundedCapacity != DataflowBlockOptions.Unbounded && BoundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BoundedCapacity),
                BoundedCapacity,
                "Bounded capacity must be greater than zero or DataflowBlockOptions.Unbounded.");
        }

        Guard.Against.NegativeOrZero(DegreeOfParallelism, nameof(DegreeOfParallelism));
        return this;
    }

    public bool Matches(PipelineExecutionOptions? other)
    {
        return other is not null &&
               BoundedCapacity == other.BoundedCapacity &&
               DegreeOfParallelism == other.DegreeOfParallelism &&
               EnsureOrdered == other.EnsureOrdered;
    }
}
